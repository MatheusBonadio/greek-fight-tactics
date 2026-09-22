using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Battle;
using Game.Content;
using Game.Core;
using Game.Grid;
using Game.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    public enum BattleFlowPhase
    {
        Exploring,
        Dialogue,
        OpponentThinking,
        PlayerChoosing,
        PlayerTargeting,
        Animating,
        Gathering
    }

    public sealed class SandboxBattleDirector : MonoBehaviour, IDuelChallenges
    {
        public const float DefaultThinkSeconds = 0.6f;
        public const int PrewarmCount = 3;
        public const string OfflineCacheFolder = "battle_cache";
        public const float MinimumVeilFadeSeconds = 0.5f;

        [SerializeField] private GridSandboxController sandbox;
        [SerializeField] private List<MapOpponent> opponents = new();
        [SerializeField] private List<ActorView> audience = new();
        [SerializeField] private BattleHud hud;
        [SerializeField] private ExplorationCamera explorationCamera;
        [SerializeField] private UiTextSO text;
        [SerializeField] private FallacyCatalogSO catalog;
        [SerializeField] private FallbackLibrarySO fallbackLibrary;
        [SerializeField] private float thinkSeconds = DefaultThinkSeconds;
        [SerializeField] private RangeHighlightView rangeView;
        [SerializeField] private Sprite playerPortrait;
        [SerializeField] private ExplorationDialogueDirector story;
        [SerializeField] private BattleCrowd crowd;
        [SerializeField] private ArenaVeilView veil;

        private readonly List<Challenger> challengers = new();
        private readonly Dictionary<ActorView, FallacyRevealView> reveals = new();
        private GameServices services;
        private ExplorationDialogueDirector storyteller;
        private StoryEffect? duelResult;
        private RangeHighlighter highlighter;
        private PauseMenuController pause;
        private BattleMenuInput menuInput;
        private InputAction confirmAction;
        private InputAction cancelAction;
        private InputAction navigateAction;
        private DialoguePlayer scripts;
        private RefuteTutorial tutorial;
        private OpponentTutorial tutorialData;
        private int targetingFrame = -1;
        private BattleMenuController controller;
        private FallacyRevealView reveal;
        private Action dialogueContinuation;
        private Action animationContinuation;
        private ActorView movingActor;
        private float thinkTimer;
        private int dialogueFrame = -1;
        private MapOpponent engaged;
        private PrewarmedArgumentSource preparedArguments;
        private int crowdSeed;
        private BattleFlowPhase phase = BattleFlowPhase.Exploring;

        public GameStateMachine StateMachine => sandbox.StateMachine;

        public BattleFlowPhase Phase
        {
            get => phase;
            private set
            {
                phase = value;
                if (hud && hud.Menu)
                {
                    hud.Menu.SetInteractive(value == BattleFlowPhase.PlayerChoosing);
                }
            }
        }

        public BattleSession Session { get; private set; }

        public BattleMenuController Controller => controller;

        public BattleHud Hud => hud;

        public IReadOnlyList<MapOpponent> Opponents => opponents;

        public MapOpponent Engaged => engaged;

        public ActorView OpponentActor => engaged?.Actor;

        public OpponentSO Opponent => engaged?.Opponent;

        public Sprite OpponentPortrait => engaged?.Portrait;

        public RangeHighlightView RangeView => rangeView;

        public Sprite PlayerPortrait => playerPortrait;

        public bool AutosaveEnabled { get; set; } = true;

        public BattleStatus LastResult { get; private set; } = BattleStatus.Ongoing;

        public PrewarmedArgumentSource PreparedArguments => preparedArguments;

        public ExplorationDialogueDirector Story => story;

        public RefuteTutorial Tutorial => tutorial;

        public BattleCrowd Crowd => crowd;

        public ArenaVeilView Veil => veil;

        public ExplorationCamera ExplorationCamera => explorationCamera;

        public DuelArena Arena { get; private set; }

        public int? CrowdSeed { get; set; }

        public bool IsPlayingScript => scripts != null && scripts.IsPlaying;

        public string ActiveScriptId => scripts?.Run?.Script.Id;

        public float ThinkSeconds
        {
            get => thinkSeconds;
            set => thinkSeconds = Mathf.Max(0f, value);
        }

        public void Configure(
            GridSandboxController sandboxController,
            List<MapOpponent> mapOpponents,
            List<ActorView> audienceViews,
            BattleHud battleHud,
            ExplorationCamera camera,
            UiTextSO uiText,
            FallacyCatalogSO fallacies,
            FallbackLibrarySO fallbacks)
        {
            sandbox = sandboxController;
            opponents = mapOpponents;
            audience = audienceViews;
            hud = battleHud;
            explorationCamera = camera;
            text = uiText;
            catalog = fallacies;
            fallbackLibrary = fallbacks;
        }

        public void ConfigureRange(RangeHighlightView view)
        {
            rangeView = view;
        }

        public void ConfigurePlayerPortrait(Sprite player)
        {
            playerPortrait = player;
        }

        public void ConfigureStory(ExplorationDialogueDirector storyDirector)
        {
            story = storyDirector;
        }

        public void ConfigureArena(BattleCrowd battleCrowd, ArenaVeilView arenaVeil)
        {
            crowd = battleCrowd;
            veil = arenaVeil;
        }

        public bool IsDefeated(MapOpponent entry) =>
            entry != null && entry.Opponent && sandbox && sandbox.Duels.HasWon(entry.Opponent.Id);

        public ActorView ChallengerAt(Coord cell)
        {
            if (Phase != BattleFlowPhase.Exploring || !sandbox.ProgressLoaded || (pause && pause.IsPaused))
            {
                return null;
            }

            CollectChallengers();
            var index = ChallengeRule.ChallengerAt(cell, StateMachine.Mode, challengers);
            return index == ChallengeRule.NoChallenger ? null : opponents[index].Actor;
        }

        public void Challenge(ActorView challenger)
        {
            var entry = opponents.Find(candidate => candidate != null && candidate.IsPlaced && candidate.Actor == challenger);
            if (entry != null && ChallengerAt(entry.Actor.Walker.Cell) == challenger)
            {
                StartBattle(entry);
            }
        }

        public void StartBattle(MapOpponent target)
        {
            if (Phase != BattleFlowPhase.Exploring || target == null || !target.IsPlaced || sandbox.Player?.Walker == null)
            {
                return;
            }

            engaged = target;
            var opponent = target.Opponent;
            var playerWalker = sandbox.Player.Walker;
            var opponentWalker = target.Actor.Walker;
            playerWalker.Cancel();
            sandbox.ExplorationInputEnabled = false;
            if (pause)
            {
                pause.Input.Panel = null;
            }

            StateMachine.TryTransition(GameMode.BattleIntro);
            var seed = Environment.TickCount;
            GatherCrowd(playerWalker.Cell, opponentWalker.Cell, CrowdSeed ?? seed);
            tutorialData = PendingTutorial(opponent);
            tutorial = tutorialData != null ? new RefuteTutorial() : null;
            Session = BattleSession.Start(sandbox.Grid, new BattleSetup
            {
                PlayerId = playerWalker.ActorId,
                PlayerCell = playerWalker.Cell,
                PlayerFacing = playerWalker.Facing,
                OpponentId = opponentWalker.ActorId,
                OpponentCell = opponentWalker.Cell,
                OpponentFacing = opponentWalker.Facing,
                OpponentProfile = opponent.Profile,
                OpponentMaxCoherence = opponent.MaxCoherence,
                InitialAudience = opponent.InitialAudience,
                Act = opponent.Act,
                AudienceIds = audience.Where(view => view && view.Walker != null).Select(view => view.Walker.ActorId).ToList(),
                LockedActions = ActionUnlocks.LockedIn(CurrentStory(), ActionMenuModel.PlayerActions),
                Spectators = crowd ? crowd.Reservations : Array.Empty<KeyValuePair<int, Coord>>(),
                Seed = seed,
                Rules = tutorial != null ? RefuteTutorial.Rules(opponent.MaxCoherence) : null
            },
            tutorial != null ? TutorialArguments(tutorialData) : CreateArgumentSource(target, seed),
            tutorial != null ? new TutorialOpponent() : null);

            controller = new BattleMenuController(Session.Menu, catalog.RefutableIds);
            hud.gameObject.SetActive(true);
            hud.Bind(Session.State, controller);
            hud.Introduce(new DebaterBadge(text.Get(BattleText.PlayerNameKey), playerPortrait), new DebaterBadge(opponent.DisplayName, target.Portrait));
            if (services)
            {
                hud.ApplySettings(services.Settings);
            }

            menuInput = hud.GetComponent<BattleMenuInput>();
            menuInput.enabled = false;
            hud.Menu.ChoiceMade += OnChoiceMade;

            highlighter = new RangeHighlighter(Session.State.Grid, new PathFinder());
            if (rangeView)
            {
                rangeView.Bind(highlighter);
            }

            reveal = RevealFor(target.Actor);
            reveal.Hide();

            if (tutorial != null && TryPlayCue(tutorial.OnBattleStart(), BeginFirstTurn))
            {
                return;
            }

            ShowLine(string.Empty, string.Format(text.Get(BattleText.IntroKey), opponent.DisplayName), BeginFirstTurn);
        }

        public void SubmitChoice(BattleMenuChoice choice)
        {
            OnChoiceMade(choice);
        }

        public bool TryChooseDestination(Coord cell)
        {
            if (Phase != BattleFlowPhase.PlayerTargeting || !CirculateTargeting.IsValidDestination(highlighter, Session.State, cell))
            {
                return false;
            }

            EndTargeting();
            OnChoiceMade(new BattleMenuChoice(ActionId.Circulate, destination: cell));
            return true;
        }

        public void CancelTargeting()
        {
            if (Phase != BattleFlowPhase.PlayerTargeting)
            {
                return;
            }

            EndTargeting();
            hud.BindPreview(highlighter);
            menuInput.enabled = true;
        }

        public void AdvanceDialogue()
        {
            if (Phase != BattleFlowPhase.Dialogue)
            {
                return;
            }

            if (IsPlayingScript)
            {
                scripts.Confirm();
                return;
            }

            hud.Dialogue.Confirm();
            if (hud.Dialogue.IsOpen)
            {
                return;
            }

            var next = dialogueContinuation;
            dialogueContinuation = null;
            next?.Invoke();
        }

        private void Awake()
        {
            services = FindAnyObjectByType<GameServices>();
            storyteller = FindAnyObjectByType<ExplorationDialogueDirector>();
            pause = FindAnyObjectByType<PauseMenuController>();
            if (!story)
            {
                story = FindAnyObjectByType<ExplorationDialogueDirector>();
            }

            if (story)
            {
                story.Challenges = this;
            }

            confirmAction = sandbox.Controls.FindAction(BattleMenuInput.ConfirmAction, true);
            cancelAction = sandbox.Controls.FindAction(BattleMenuInput.CancelAction, true);
            navigateAction = sandbox.Controls.FindAction(BattleMenuInput.NavigateAction, true);
            if (hud)
            {
                hud.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (hud)
            {
                hud.Menu.ChoiceMade -= OnChoiceMade;
            }

            if (story && ReferenceEquals(story.Challenges, this))
            {
                story.Challenges = null;
            }

            scripts?.Dispose();
        }

        private void Update()
        {
            switch (Phase)
            {
                case BattleFlowPhase.Exploring:
                    PrepareArguments();
                    break;
                case BattleFlowPhase.Dialogue:
                    if (Time.frameCount == dialogueFrame)
                    {
                        break;
                    }

                    if (IsPlayingScript)
                    {
                        scripts.Poll(confirmAction, navigateAction);
                    }
                    else if (confirmAction.WasPressedThisFrame())
                    {
                        AdvanceDialogue();
                    }

                    break;
                case BattleFlowPhase.PlayerChoosing:
                    HintFallaciesDuringTutorial();
                    break;
                case BattleFlowPhase.OpponentThinking:
                    thinkTimer -= Time.deltaTime;
                    if (thinkTimer <= 0f)
                    {
                        AfterOutcome(engaged.Actor, Session.PlayOpponentTurn(), engaged.Opponent.DisplayName);
                    }

                    break;
                case BattleFlowPhase.PlayerTargeting:
                    if (Time.frameCount == targetingFrame)
                    {
                        break;
                    }

                    if (confirmAction.WasPressedThisFrame())
                    {
                        TryChooseDestination(sandbox.Cursor.Cell);
                    }
                    else if (cancelAction.WasPressedThisFrame())
                    {
                        CancelTargeting();
                    }

                    break;
                case BattleFlowPhase.Gathering:
                    if (!crowd || crowd.HasArrived)
                    {
                        StartTurns();
                    }

                    break;
                case BattleFlowPhase.Animating:
                    if (movingActor == null || movingActor.Walker == null || !movingActor.Walker.IsMoving)
                    {
                        movingActor = null;
                        var next = animationContinuation;
                        animationContinuation = null;
                        next?.Invoke();
                    }

                    break;
            }
        }

        private void CollectChallengers()
        {
            challengers.Clear();
            foreach (var entry in opponents)
            {
                if (entry == null || !entry.IsPlaced)
                {
                    challengers.Add(Challenger.Absent);
                    continue;
                }

                var cell = entry.Actor.Walker.Cell;
                challengers.Add(new Challenger(cell, FogTileRules.ShowsWhatStandsOn(sandbox.Grid, cell), IsDefeated(entry)));
            }
        }

        private void PrepareArguments()
        {
            if (preparedArguments != null || !services || !services.IsBooted || !sandbox.ProgressLoaded)
            {
                return;
            }

            preparedArguments = services.CreateArgumentSource(Environment.TickCount);
            foreach (var entry in opponents)
            {
                if (entry == null || !entry.IsPlaced)
                {
                    continue;
                }

                preparedArguments.Register(entry.Actor.Walker.ActorId, OpponentContext.From(entry.Opponent));
                if (!IsDefeated(entry))
                {
                    TopUpArguments(entry);
                }
            }
        }

        private void TopUpArguments(MapOpponent entry)
        {
            if (preparedArguments == null || entry == null || !entry.IsPlaced)
            {
                return;
            }

            var opponentId = entry.Actor.Walker.ActorId;
            var missing = PrewarmCount - preparedArguments.ReadyCount(opponentId);
            if (missing > 0)
            {
                _ = services.PrewarmAsync(preparedArguments, opponentId, missing);
            }
        }

        private IArgumentSource CreateArgumentSource(MapOpponent target, int seed)
        {
            PrepareArguments();
            if (preparedArguments != null)
            {
                return preparedArguments;
            }

            var validator = new ArgumentValidator(catalog.RefutableIds);
            var offline = new PrewarmedArgumentSource(
                null,
                new ArgumentDiskCache(Path.Combine(Application.temporaryCachePath, OfflineCacheFolder)),
                validator,
                new FallbackArgumentLibrary(fallbackLibrary, validator, seed),
                seed);
            offline.Register(target.Actor.Walker.ActorId, OpponentContext.From(target.Opponent));
            return offline;
        }

        private IArgumentSource TutorialArguments(OpponentTutorial data)
        {
            var arguments = new List<Argument>();
            if (fallbackLibrary.TryGetJson(data.FallacyId, out var json))
            {
                ArgumentJson.ParseValidList(json, new ArgumentValidator(catalog.RefutableIds), arguments);
            }

            return new ScriptedArgumentSource(arguments);
        }

        private OpponentTutorial PendingTutorial(OpponentSO opponent)
        {
            var data = opponent ? opponent.Tutorial : null;
            if (data == null || !data.IsDefined || !story || !story.Library || !hud.Dialogue)
            {
                return null;
            }

            return data.IsPending(story.LoadStory()) ? data : null;
        }

        private bool TryPlayCue(TutorialCue cue, Action continuation)
        {
            var dialogueId = cue == TutorialCue.None ? null : tutorialData?.DialogueFor(cue);
            var storyData = dialogueId != null && story ? story.LoadStory() : null;
            if (storyData == null || !story.Library.TryGet(dialogueId, out var script))
            {
                return false;
            }

            var cueRun = script.Begin(storyData);
            scripts ??= new DialoguePlayer(hud.Dialogue, story.SpeakerName, story.PortraitFor);
            if (cueRun.IsEmpty || scripts.IsPlaying)
            {
                return false;
            }

            Phase = BattleFlowPhase.Dialogue;
            dialogueFrame = Time.frameCount;
            dialogueContinuation = null;
            return scripts.TryPlay(cueRun, run =>
            {
                story.Record(run.AppliedEffects);
                continuation();
            });
        }

        private void PlayCue(TutorialCue cue, Action continuation)
        {
            if (!TryPlayCue(cue, continuation))
            {
                continuation();
            }
        }

        private void HintFallaciesDuringTutorial()
        {
            if (tutorial == null || controller == null || controller.Layer != BattleMenuLayer.Fallacies)
            {
                return;
            }

            var cue = tutorial.OnFallaciesOpened();
            if (cue == TutorialCue.None)
            {
                return;
            }

            menuInput.enabled = false;
            PlayCue(cue, ResumeChoosing);
        }

        private FallacyRevealView RevealFor(ActorView actor)
        {
            if (!reveals.TryGetValue(actor, out var view) || !view)
            {
                view = FallacyRevealView.Attach(actor);
                reveals[actor] = view;
            }

            return view;
        }

        private void GatherCrowd(Coord player, Coord opponent, int seed)
        {
            crowdSeed = seed;
            Arena = DuelCrowdPlanner.Plan(sandbox.Grid, player, opponent, crowd ? crowd.Rules : null, seed);
            if (crowd)
            {
                crowd.OutsideMaterial = veil ? veil.ActorMaterial : null;
                crowd.Gather(sandbox.Grid, Arena);
            }

            if (explorationCamera)
            {
                explorationCamera.FrameDuel(Arena.WorldCenter);
            }

            if (!veil)
            {
                return;
            }

            var outside = sandbox.Actors
                .Where(actor => actor && actor.gameObject.activeInHierarchy && actor.Walker != null && actor != sandbox.Player && actor != engaged.Actor && !Arena.IsLit(actor.Walker.Cell))
                .Select(actor => actor.SpriteRenderer)
                .ToList();
            var fadeIn = Mathf.Max(MinimumVeilFadeSeconds, crowd ? crowd.ExpectedArrivalSeconds : 0f);
            veil.Show(sandbox.Grid.Width, sandbox.Grid.Height, Arena.IsLit, outside, fadeIn);
        }

        private void DisperseCrowd()
        {
            if (crowd)
            {
                crowd.Disperse(unchecked(crowdSeed + 1));
            }

            if (veil)
            {
                veil.Hide();
            }

            if (explorationCamera)
            {
                explorationCamera.FrameExploration();
            }

            Arena = null;
        }

        private void BeginFirstTurn()
        {
            if (crowd && !crowd.HasArrived)
            {
                Phase = BattleFlowPhase.Gathering;
                return;
            }

            StartTurns();
        }

        private void StartTurns()
        {
            StateMachine.TryTransition(GameMode.BattleTurn);
            NextTurn();
        }

        private void NextTurn()
        {
            hud.Sync();
            if (Session.IsOver)
            {
                EndBattle();
                return;
            }

            if (Session.IsPlayerTurn)
            {
                PlayCue(tutorial?.OnPlayerTurn(Session.State) ?? TutorialCue.None, BeginChoosing);
                return;
            }

            HideRange();
            menuInput.enabled = false;
            thinkTimer = thinkSeconds;
            Phase = BattleFlowPhase.OpponentThinking;
        }

        private void BeginChoosing()
        {
            Phase = BattleFlowPhase.PlayerChoosing;
            controller.Refresh();
            hud.Menu.Render();
            hud.BindPreview(highlighter);
            menuInput.enabled = true;
        }

        private void ResumeChoosing()
        {
            Phase = BattleFlowPhase.PlayerChoosing;
            hud.Menu.Render();
            menuInput.enabled = true;
        }

        private void ContinueAfter(Outcome outcome)
        {
            PlayCue(tutorial?.OnOutcome(outcome, Session.State) ?? TutorialCue.None, NextTurn);
        }

        private void OnChoiceMade(BattleMenuChoice choice)
        {
            if (Phase != BattleFlowPhase.PlayerChoosing || Session == null)
            {
                return;
            }

            if (choice.Action == ActionId.Circulate && !choice.Destination.HasValue)
            {
                BeginTargeting();
                return;
            }

            menuInput.enabled = false;
            HideRange();
            var outcome = Session.PlayChoice(choice);
            if (outcome.IsRejected)
            {
                ShowLine(text.Get(BattleText.PlayerNameKey), playerPortrait, text.Get(UiTextKeys.For(outcome.Rejection)), NextTurn);
                return;
            }

            if (outcome.Action == ActionId.Refute && outcome.Hit && catalog.TryGet(choice.FallacyId, out var fallacy))
            {
                reveal.Reveal(fallacy);
                if (services)
                {
                    services.Compendium.Unlock(fallacy.Id);
                }
            }

            AfterOutcome(sandbox.Player, outcome, text.Get(BattleText.PlayerNameKey));
        }

        private void AfterOutcome(ActorView actor, Outcome outcome, string speaker)
        {
            hud.Sync();

            void Continue() => ContinueAfter(outcome);

            void Speak()
            {
                if (outcome.PresentedArgument.HasValue)
                {
                    ShowLine(engaged.Opponent.DisplayName, engaged.Portrait, outcome.PresentedArgument.Value.Speech, Continue);
                    return;
                }

                var key = BattleText.OutcomeKey(outcome);
                if (key == null)
                {
                    Continue();
                    return;
                }

                ShowLine(string.Empty, string.Format(text.Get(key), speaker), Continue);
            }

            if (outcome.Moves && actor?.Walker != null)
            {
                actor.Walker.Follow(outcome.Path);
                movingActor = actor;
                animationContinuation = Speak;
                Phase = BattleFlowPhase.Animating;
                return;
            }

            Speak();
        }

        private void BeginTargeting()
        {
            var player = Session.State.Player.Position;
            menuInput.enabled = false;
            hud.BindPreview(null);
            highlighter.ShowMove(player, Session.State.Rules.MoveRange);
            sandbox.Cursor.TryPoint(player);
            sandbox.CursorInputEnabled = true;
            targetingFrame = Time.frameCount;
            Phase = BattleFlowPhase.PlayerTargeting;
        }

        private void EndTargeting()
        {
            sandbox.CursorInputEnabled = false;
            Phase = BattleFlowPhase.PlayerChoosing;
        }

        private void HideRange()
        {
            hud.BindPreview(null);
            highlighter?.Clear();
        }

        private void ShowLine(string speaker, string body, Action continuation) => ShowLine(speaker, null, body, continuation);

        private void ShowLine(string speaker, Sprite portrait, string body, Action continuation)
        {
            Phase = BattleFlowPhase.Dialogue;
            dialogueFrame = Time.frameCount;
            dialogueContinuation = continuation;
            hud.Dialogue.Show(new DialogueLine(speaker, portrait, body));
        }

        private void EndBattle()
        {
            LastResult = Session.State.Status;
            if (DuelVictoryRule.TryClassify(Session.State, out var victory))
            {
                sandbox.Duels.RecordVictory(engaged.Opponent.Id, victory);
            }

            var result = DuelStoryResults.ResultOf(engaged.Opponent.Id, LastResult);
            duelResult = result;
            if (storyteller)
            {
                storyteller.Remember(result);
            }

            StateMachine.TryTransition(GameMode.BattleResolve);
            StateMachine.TryTransition(GameMode.BattleOutro);
            var cue = tutorial?.OnBattleOver(Session.State) ?? TutorialCue.None;
            if (cue != TutorialCue.None)
            {
                story.Remember(new StoryEffect(tutorialData.DoneFlag));
            }

            if (TryPlayCue(cue, FinishBattle))
            {
                return;
            }

            ShowLine(string.Empty, text.Get(BattleText.StatusKey(LastResult)), FinishBattle);
        }

        private void FinishBattle()
        {
            hud.Menu.ChoiceMade -= OnChoiceMade;
            HideRange();
            if (rangeView)
            {
                rangeView.Bind(null);
            }

            highlighter = null;
            hud.gameObject.SetActive(false);
            reveal?.Hide();

            if (!IsDefeated(engaged))
            {
                TopUpArguments(engaged);
            }

            var from = StateMachine.Mode;
            StateMachine.TryTransition(GameMode.Exploration);
            DisperseCrowd();
            sandbox.CursorInputEnabled = false;
            sandbox.ExplorationInputEnabled = true;
            if (pause)
            {
                pause.Input.Panel = pause;
            }

            Phase = BattleFlowPhase.Exploring;
            Session = null;
            controller = null;
            tutorial = null;
            tutorialData = null;

            var result = duelResult;
            duelResult = null;
            if (AutosaveEnabled && services && services.IsBooted && AutosavePolicy.ShouldSave(from, GameMode.Exploration))
            {
                var save = story ? story.CaptureSave() : sandbox.CaptureSave();
                if (result.HasValue)
                {
                    result.Value.TryApply(save, out _);
                }

                services.Autosave(save);
            }
        }

        private SaveData CurrentStory()
        {
            if (storyteller && storyteller.IsStoryLoaded)
            {
                return storyteller.Story;
            }

            return services && services.IsBooted && services.TryLoadActiveSave(out var save) ? save : new SaveData();
        }
    }
}
