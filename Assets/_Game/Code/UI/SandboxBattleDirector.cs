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
        Animating
    }

    public sealed class SandboxBattleDirector : MonoBehaviour
    {
        public const float DefaultThinkSeconds = 0.6f;
        public const int PrewarmCount = 3;
        public const string OfflineCacheFolder = "battle_cache";

        [SerializeField] private GridSandboxController sandbox;
        [SerializeField] private ActorView opponentActor;
        [SerializeField] private List<ActorView> audience = new();
        [SerializeField] private OpponentSO opponent;
        [SerializeField] private BattleHud hud;
        [SerializeField] private ExplorationCamera explorationCamera;
        [SerializeField] private UiTextSO text;
        [SerializeField] private FallacyCatalogSO catalog;
        [SerializeField] private FallbackLibrarySO fallbackLibrary;
        [SerializeField] private int aggroRadius = AggroRule.DefaultRadius;
        [SerializeField] private float thinkSeconds = DefaultThinkSeconds;
        [SerializeField] private RangeHighlightView rangeView;

        private GameServices services;
        private RangeHighlighter highlighter;
        private PauseMenuController pause;
        private BattleMenuInput menuInput;
        private InputAction confirmAction;
        private BattleMenuController controller;
        private FallacyRevealView reveal;
        private Action dialogueContinuation;
        private Action animationContinuation;
        private ActorView movingActor;
        private float thinkTimer;
        private int dialogueFrame = -1;
        private bool waitingForAggroExit;

        public GameStateMachine StateMachine { get; } = new();

        public BattleFlowPhase Phase { get; private set; } = BattleFlowPhase.Exploring;

        public BattleSession Session { get; private set; }

        public BattleMenuController Controller => controller;

        public BattleHud Hud => hud;

        public ActorView OpponentActor => opponentActor;

        public OpponentSO Opponent => opponent;

        public RangeHighlightView RangeView => rangeView;

        public bool AutosaveEnabled { get; set; } = true;

        public bool OpponentDefeated { get; private set; }

        public BattleStatus LastResult { get; private set; } = BattleStatus.Ongoing;

        public float ThinkSeconds
        {
            get => thinkSeconds;
            set => thinkSeconds = Mathf.Max(0f, value);
        }

        public void Configure(
            GridSandboxController sandboxController,
            ActorView opponentView,
            List<ActorView> audienceViews,
            OpponentSO opponentData,
            BattleHud battleHud,
            ExplorationCamera camera,
            UiTextSO uiText,
            FallacyCatalogSO fallacies,
            FallbackLibrarySO fallbacks)
        {
            sandbox = sandboxController;
            opponentActor = opponentView;
            audience = audienceViews;
            opponent = opponentData;
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

        public void StartBattle()
        {
            if (Phase != BattleFlowPhase.Exploring || sandbox.Player?.Walker == null || opponentActor?.Walker == null)
            {
                return;
            }

            var playerWalker = sandbox.Player.Walker;
            var opponentWalker = opponentActor.Walker;
            playerWalker.Cancel();
            sandbox.ExplorationInputEnabled = false;
            if (pause)
            {
                pause.Input.Panel = null;
            }

            StateMachine.TryTransition(GameMode.BattleIntro);
            if (explorationCamera)
            {
                explorationCamera.FrameBattle();
            }

            var seed = Environment.TickCount;
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
                Seed = seed
            }, CreateArgumentSource(opponentWalker.ActorId, seed));

            controller = new BattleMenuController(Session.Menu, catalog.RefutableIds);
            hud.gameObject.SetActive(true);
            hud.Bind(Session.State, controller);
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

            reveal ??= FallacyRevealView.Attach(opponentActor);
            reveal.Hide();

            ShowLine(string.Empty, string.Format(text.Get(BattleText.IntroKey), opponent.DisplayName), () =>
            {
                StateMachine.TryTransition(GameMode.BattleTurn);
                NextTurn();
            });
        }

        public void SubmitChoice(BattleMenuChoice choice)
        {
            OnChoiceMade(choice);
        }

        public void AdvanceDialogue()
        {
            if (Phase != BattleFlowPhase.Dialogue)
            {
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
            pause = FindAnyObjectByType<PauseMenuController>();
            if (pause)
            {
                pause.StateMachine = StateMachine;
            }

            confirmAction = sandbox.Controls.FindAction(BattleMenuInput.ConfirmAction, true);
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
        }

        private void Update()
        {
            switch (Phase)
            {
                case BattleFlowPhase.Exploring:
                    TryTriggerBattle();
                    break;
                case BattleFlowPhase.Dialogue:
                    if (confirmAction.WasPressedThisFrame() && Time.frameCount != dialogueFrame)
                    {
                        AdvanceDialogue();
                    }

                    break;
                case BattleFlowPhase.OpponentThinking:
                    thinkTimer -= Time.deltaTime;
                    if (thinkTimer <= 0f)
                    {
                        AfterOutcome(opponentActor, Session.PlayOpponentTurn(), opponent.DisplayName);
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

        private void TryTriggerBattle()
        {
            if (OpponentDefeated || sandbox.Player?.Walker == null || opponentActor?.Walker == null || (pause && pause.IsPaused))
            {
                return;
            }

            var inRange = AggroRule.IsTriggered(sandbox.Player.Walker.Cell, opponentActor.Walker.Cell, aggroRadius);
            if (waitingForAggroExit)
            {
                waitingForAggroExit = inRange;
                return;
            }

            if (inRange && !sandbox.Player.Walker.IsStepping)
            {
                StartBattle();
            }
        }

        private IArgumentSource CreateArgumentSource(int opponentId, int seed)
        {
            var context = OpponentContext.From(opponent);
            if (services && services.IsBooted)
            {
                var online = services.CreateArgumentSource(seed);
                online.Register(opponentId, context);
                _ = services.PrewarmAsync(online, opponentId, PrewarmCount);
                return online;
            }

            var validator = new ArgumentValidator(catalog.RefutableIds);
            var offline = new PrewarmedArgumentSource(
                null,
                new ArgumentDiskCache(Path.Combine(Application.temporaryCachePath, OfflineCacheFolder)),
                validator,
                new FallbackArgumentLibrary(fallbackLibrary, validator, seed),
                seed);
            offline.Register(opponentId, context);
            return offline;
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
                Phase = BattleFlowPhase.PlayerChoosing;
                controller.Refresh();
                hud.Menu.Render();
                hud.BindPreview(highlighter);
                menuInput.enabled = true;
                return;
            }

            HideRange();
            menuInput.enabled = false;
            thinkTimer = thinkSeconds;
            Phase = BattleFlowPhase.OpponentThinking;
        }

        private void OnChoiceMade(BattleMenuChoice choice)
        {
            if (Phase != BattleFlowPhase.PlayerChoosing || Session == null)
            {
                return;
            }

            menuInput.enabled = false;
            HideRange();
            var outcome = Session.PlayChoice(choice);
            if (outcome.IsRejected)
            {
                ShowLine(text.Get(BattleText.PlayerNameKey), text.Get(UiTextKeys.For(outcome.Rejection)), NextTurn);
                return;
            }

            if (outcome.Action == ActionId.Refute && outcome.Hit && catalog.TryGet(choice.FallacyId, out var fallacy))
            {
                reveal.Reveal(fallacy);
            }

            AfterOutcome(sandbox.Player, outcome, text.Get(BattleText.PlayerNameKey));
        }

        private void AfterOutcome(ActorView actor, Outcome outcome, string speaker)
        {
            hud.Sync();

            void Speak()
            {
                if (outcome.PresentedArgument.HasValue)
                {
                    ShowLine(opponent.DisplayName, outcome.PresentedArgument.Value.Speech, NextTurn);
                    return;
                }

                var key = BattleText.OutcomeKey(outcome);
                if (key == null)
                {
                    NextTurn();
                    return;
                }

                ShowLine(string.Empty, string.Format(text.Get(key), speaker), NextTurn);
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

        private void HideRange()
        {
            hud.BindPreview(null);
            highlighter?.Clear();
        }

        private void ShowLine(string speaker, string body, Action continuation)
        {
            Phase = BattleFlowPhase.Dialogue;
            dialogueFrame = Time.frameCount;
            dialogueContinuation = continuation;
            hud.Dialogue.Show(new DialogueLine(speaker, null, body));
        }

        private void EndBattle()
        {
            LastResult = Session.State.Status;
            StateMachine.TryTransition(GameMode.BattleResolve);
            StateMachine.TryTransition(GameMode.BattleOutro);
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

            OpponentDefeated = LastResult == BattleStatus.PlayerWon;
            waitingForAggroExit = true;

            var from = StateMachine.Mode;
            StateMachine.TryTransition(GameMode.Exploration);
            if (explorationCamera)
            {
                explorationCamera.FrameExploration();
            }

            sandbox.ExplorationInputEnabled = true;
            if (pause)
            {
                pause.Input.Panel = pause;
            }

            Phase = BattleFlowPhase.Exploring;
            Session = null;
            controller = null;

            if (AutosaveEnabled && services && services.IsBooted && AutosavePolicy.ShouldSave(from, GameMode.Exploration))
            {
                services.Autosave(sandbox.CaptureSave());
            }
        }
    }
}
