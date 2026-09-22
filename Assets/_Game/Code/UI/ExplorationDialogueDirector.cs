using System;
using System.Collections.Generic;
using Game.Content;
using Game.Core;
using Game.Grid;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    public sealed class ExplorationDialogueDirector : MonoBehaviour
    {
        [SerializeField] private GridSandboxController sandbox;
        [SerializeField] private DialogueBoxView dialogueBox;
        [SerializeField] private DialogueLibrarySO library;
        [SerializeField] private List<NpcDialogueLink> talkers = new();
        [SerializeField] private List<DialogueTriggerLink> triggers = new();
        [SerializeField] private Dictionary<string, Sprite> portraits = new();
        [SerializeField] private CutsceneDirector cutscenes;

        private readonly PathFinder pathFinder = new();
        private readonly List<Coord> candidatePath = new();
        private readonly List<Coord> approachPath = new();
        private readonly List<StoryEffect> sessionEffects = new();
        private Func<Coord, bool> interaction;
        private GameServices services;
        private PauseMenuController pause;
        private InputAction confirmAction;
        private InputAction navigateAction;
        private DialoguePlayer player;
        private NpcDialogueLink pendingTalk;
        private DialogueTriggerLink pendingTrigger;
        private GridWalker watchedWalker;
        private int openedFrame = -1;
        private int closedFrame = -1;

        public DialogueBoxView DialogueBox => dialogueBox;

        public DialogueLibrarySO Library => library;

        public IReadOnlyList<NpcDialogueLink> Talkers => talkers;

        public IReadOnlyList<DialogueTriggerLink> Triggers => triggers;

        public IReadOnlyDictionary<string, Sprite> Portraits => portraits;

        public CutsceneDirector Cutscenes => cutscenes;

        public bool AutosaveEnabled { get; set; } = true;

        public SaveData Story { get; private set; }

        public bool IsStoryLoaded => Story != null;

        public bool IsTalking => player != null && player.IsPlaying;

        public bool IsAwaitingChoice => player != null && player.IsAwaitingChoice;

        public bool IsApproaching => pendingTalk != null;

        public bool HasPendingTrigger => pendingTrigger != null;

        public string ActiveDialogueId => player?.Run?.Script.Id;

        public ScriptLine CurrentLine => player?.Run?.Current;

        public void Configure(
            GridSandboxController controller,
            DialogueBoxView box,
            DialogueLibrarySO dialogues,
            List<NpcDialogueLink> npcs,
            List<DialogueTriggerLink> cellTriggers,
            Dictionary<string, Sprite> speakerPortraits)
        {
            sandbox = controller;
            dialogueBox = box;
            library = dialogues;
            talkers = npcs;
            triggers = cellTriggers;
            portraits = speakerPortraits;
            player?.Dispose();
            player = null;
        }

        public void ConfigureCutscenes(CutsceneDirector director)
        {
            cutscenes = director;
        }

        public Sprite PortraitFor(string speakerId) =>
            !string.IsNullOrEmpty(speakerId) && portraits.TryGetValue(speakerId, out var portrait) ? portrait : null;

        public string SpeakerName(string speakerId) => library ? library.SpeakerName(speakerId) : string.Empty;

        public SaveData LoadStory()
        {
            EnsureStoryLoaded();
            return Story;
        }

        public bool Remember(StoryEffect effect)
        {
            EnsureStoryLoaded();
            if (Story == null || !effect.TryApply(Story, out _))
            {
                return false;
            }

            sessionEffects.Add(effect);
            return true;
        }

        public void Record(IEnumerable<StoryEffect> appliedEffects)
        {
            if (appliedEffects != null)
            {
                sessionEffects.AddRange(appliedEffects);
            }
        }

        public SaveData CaptureSave()
        {
            var save = sandbox.CaptureSave();
            foreach (var effect in sessionEffects)
            {
                effect.TryApply(save, out _);
            }

            return save;
        }

        public bool HandleConfirm(Coord cell)
        {
            if (IsTalking || Time.frameCount == closedFrame)
            {
                return true;
            }

            var talker = TalkerAt(cell);
            if (talker == null)
            {
                pendingTalk = null;
                return TryApproachTriggerBeside(cell);
            }

            Approach(talker);
            return true;
        }

        public bool TryStartDialogue(string dialogueId)
        {
            EnsureStoryLoaded();
            if (IsTalking || Story == null || !library || !CanOpenFrom(sandbox.StateMachine.Mode) || !library.TryGet(dialogueId, out var script))
            {
                return false;
            }

            var candidate = script.Begin(Story);
            if (candidate.IsEmpty || !sandbox.StateMachine.TryOpenOverlay(GameMode.Dialogue))
            {
                return false;
            }

            pendingTalk = null;
            sandbox.Player?.Walker?.Cancel();
            if (pause)
            {
                pause.Input.Panel = null;
            }

            if (services)
            {
                dialogueBox.CharactersPerSecond = services.Settings.CharactersPerSecond;
            }

            openedFrame = Time.frameCount;
            Player.TryPlay(candidate, Finish);
            return true;
        }

        public bool TryPlayOnce(string dialogueId, string onceFlag)
        {
            EnsureStoryLoaded();
            if (IsTalking || Story == null || StoryCondition.Has(onceFlag).IsMet(Story))
            {
                return false;
            }

            Remember(new StoryEffect(onceFlag));
            if (!TryStartDialogue(dialogueId))
            {
                SaveProgress();
            }

            return true;
        }

        public bool TryPlayCutsceneOnce(string cutsceneId, string onceFlag)
        {
            EnsureStoryLoaded();
            if (!cutscenes || IsTalking || Story == null || StoryCondition.Has(onceFlag).IsMet(Story) || !cutscenes.TryPlay(cutsceneId))
            {
                return false;
            }

            Remember(new StoryEffect(onceFlag));
            return true;
        }

        public void Advance()
        {
            if (IsTalking)
            {
                player.Confirm();
            }
        }

        private DialoguePlayer Player => player ??= new DialoguePlayer(dialogueBox, SpeakerName, PortraitFor);

        private void Awake()
        {
            services = FindAnyObjectByType<GameServices>();
            pause = FindAnyObjectByType<PauseMenuController>();
            confirmAction = sandbox.Controls.FindAction(BattleMenuInput.ConfirmAction, true);
            navigateAction = sandbox.Controls.FindAction(BattleMenuInput.NavigateAction, true);
            interaction = HandleConfirm;
            sandbox.Interaction = interaction;
        }

        private void Start()
        {
            watchedWalker = sandbox.Player?.Walker;
            if (watchedWalker != null)
            {
                watchedWalker.CellEntered += OnPlayerCellEntered;
            }
        }

        private void OnDestroy()
        {
            if (watchedWalker != null)
            {
                watchedWalker.CellEntered -= OnPlayerCellEntered;
            }

            if (sandbox && sandbox.Interaction == interaction)
            {
                sandbox.Interaction = null;
            }

            player?.Dispose();
        }

        private void OnPlayerCellEntered(Coord cell)
        {
            if (IsTalking || !sandbox.ExplorationInputEnabled || sandbox.StateMachine.Mode != GameMode.Exploration || !TryFindUnfiredTrigger(cell, out var trigger))
            {
                return;
            }

            pendingTalk = null;
            pendingTrigger = trigger;
            watchedWalker.Cancel();
        }

        private void Update()
        {
            EnsureStoryLoaded();
            if (IsTalking)
            {
                if (sandbox.StateMachine.Mode == GameMode.Dialogue && Time.frameCount != openedFrame)
                {
                    player.Poll(confirmAction, navigateAction);
                }

                return;
            }

            var walker = sandbox.Player?.Walker;
            if (walker == null || !sandbox.ExplorationInputEnabled || sandbox.StateMachine.Mode != GameMode.Exploration)
            {
                if (sandbox.StateMachine.IsInBattle)
                {
                    pendingTalk = null;
                    pendingTrigger = null;
                }

                return;
            }

            if (walker.IsMoving)
            {
                return;
            }

            if (pendingTrigger != null)
            {
                var trigger = pendingTrigger;
                pendingTrigger = null;
                if (trigger.Cell == walker.Cell)
                {
                    Fire(trigger);
                }

                return;
            }

            if (pendingTalk == null)
            {
                return;
            }

            var talker = pendingTalk;
            pendingTalk = null;
            if (IsBeside(walker.Cell, talker))
            {
                Converse(talker);
            }
        }

        private void EnsureStoryLoaded()
        {
            if (Story != null)
            {
                return;
            }

            if (!services)
            {
                Story = new SaveData();
                return;
            }

            if (services.IsBooted)
            {
                Story = services.TryLoadActiveSave(out var save) ? save : new SaveData();
            }
        }

        private void Approach(NpcDialogueLink talker)
        {
            pendingTalk = null;
            var walker = sandbox.Player?.Walker;
            if (walker == null)
            {
                return;
            }

            if (IsBeside(walker.Cell, talker))
            {
                walker.Cancel();
                pendingTalk = talker;
                return;
            }

            if (TryFindApproach(walker.Cell, talker.Actor.Walker.Cell))
            {
                walker.Follow(approachPath);
                pendingTalk = talker;
            }
        }

        private void Converse(NpcDialogueLink talker)
        {
            var walker = sandbox.Player.Walker;
            var npc = talker.Actor.Walker;
            walker.FaceToward(npc.Cell);
            npc.FaceToward(walker.Cell);
            TryStartDialogue(talker.Dialogue);
        }

        private void Fire(DialogueTriggerLink trigger)
        {
            if (trigger.IsCutscene)
            {
                TryPlayCutsceneOnce(trigger.Cutscene, trigger.Flag);
                return;
            }

            FaceSpeakerBeside(trigger.Dialogue);
            TryPlayOnce(trigger.Dialogue, trigger.Flag);
        }

        private void FaceSpeakerBeside(string dialogueId)
        {
            var walker = sandbox.Player.Walker;
            var talker = talkers.Find(candidate => candidate.Dialogue == dialogueId && CanTalkTo(candidate) && IsBeside(walker.Cell, candidate));
            if (talker == null)
            {
                return;
            }

            walker.FaceToward(talker.Actor.Walker.Cell);
            talker.Actor.Walker.FaceToward(walker.Cell);
        }

        private void Finish(DialogueRun finished)
        {
            sessionEffects.AddRange(finished.AppliedEffects);
            closedFrame = Time.frameCount;

            var from = sandbox.StateMachine.Mode;
            sandbox.StateMachine.TryCloseOverlay();
            if (pause && sandbox.StateMachine.Mode == GameMode.Exploration)
            {
                pause.Input.Panel = pause;
            }

            if (AutosavePolicy.ShouldSave(from, sandbox.StateMachine.Mode))
            {
                SaveProgress();
            }
        }

        public void SaveProgress()
        {
            if (AutosaveEnabled && services && services.IsBooted)
            {
                services.Autosave(CaptureSave());
            }
        }

        private static bool CanOpenFrom(GameMode mode) => mode is GameMode.Exploration or GameMode.Cutscene;

        private bool CanTalkTo(NpcDialogueLink talker) =>
            talker.Actor && talker.Actor.Walker != null && library && library.Contains(talker.Dialogue);

        private NpcDialogueLink TalkerAt(Coord cell) =>
            talkers.Find(talker => CanTalkTo(talker) && talker.Actor.Walker.Cell == cell);

        private static bool IsBeside(Coord cell, NpcDialogueLink talker) => AreBeside(cell, talker.Actor.Walker.Cell);

        private static bool AreBeside(Coord first, Coord second) => Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y) == 1;

        private bool TryApproachTriggerBeside(Coord occupiedCell)
        {
            var walker = sandbox.Player?.Walker;
            EnsureStoryLoaded();
            if (walker == null || Story == null || occupiedCell == walker.Cell || !sandbox.Grid.TryGet(occupiedCell, out var tile) || !tile.IsOccupied)
            {
                return false;
            }

            approachPath.Clear();
            DialogueTriggerLink nearest = null;
            foreach (var trigger in triggers)
            {
                if (!AreBeside(trigger.Cell, occupiedCell) || !trigger.IsDue(Story))
                {
                    continue;
                }

                if (trigger.Cell == walker.Cell)
                {
                    walker.Cancel();
                    pendingTrigger = trigger;
                    return true;
                }

                if (!pathFinder.FindPath(sandbox.Grid, walker.Cell, trigger.Cell, candidatePath) || (nearest != null && candidatePath.Count >= approachPath.Count))
                {
                    continue;
                }

                nearest = trigger;
                approachPath.Clear();
                approachPath.AddRange(candidatePath);
            }

            if (nearest == null)
            {
                return false;
            }

            walker.Follow(approachPath);
            return true;
        }

        private bool TryFindApproach(Coord from, Coord target)
        {
            approachPath.Clear();
            var found = false;
            foreach (var beside in new[] { new Coord(target.X + 1, target.Y), new Coord(target.X - 1, target.Y), new Coord(target.X, target.Y + 1), new Coord(target.X, target.Y - 1) })
            {
                if (!pathFinder.FindPath(sandbox.Grid, from, beside, candidatePath) || (found && candidatePath.Count >= approachPath.Count))
                {
                    continue;
                }

                approachPath.Clear();
                approachPath.AddRange(candidatePath);
                found = true;
            }

            return found;
        }

        private bool TryFindUnfiredTrigger(Coord cell, out DialogueTriggerLink trigger)
        {
            trigger = null;
            if (Story == null)
            {
                return false;
            }

            trigger = triggers.Find(candidate => candidate.Cell == cell && candidate.IsDue(Story));
            return trigger != null;
        }
    }
}
