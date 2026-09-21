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

        private readonly PathFinder pathFinder = new();
        private readonly List<Coord> candidatePath = new();
        private readonly List<Coord> approachPath = new();
        private readonly List<StoryEffect> sessionEffects = new();
        private Func<Coord, bool> interaction;
        private GameServices services;
        private PauseMenuController pause;
        private InputAction confirmAction;
        private DialogueRun run;
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

        public bool AutosaveEnabled { get; set; } = true;

        public SaveData Story { get; private set; }

        public bool IsStoryLoaded => Story != null;

        public bool IsTalking => run != null;

        public bool IsApproaching => pendingTalk != null;

        public string ActiveDialogueId => run?.Script.Id;

        public ScriptLine CurrentLine => run?.Current;

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
        }

        public Sprite PortraitFor(string speakerId) =>
            !string.IsNullOrEmpty(speakerId) && portraits.TryGetValue(speakerId, out var portrait) ? portrait : null;

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
                return false;
            }

            Approach(talker);
            return true;
        }

        public bool TryStartDialogue(string dialogueId)
        {
            EnsureStoryLoaded();
            if (IsTalking || Story == null || !library || sandbox.StateMachine.Mode != GameMode.Exploration || !library.TryGet(dialogueId, out var script))
            {
                return false;
            }

            var candidate = script.Begin(Story);
            if (candidate.IsEmpty || !sandbox.StateMachine.TryOpenOverlay(GameMode.Dialogue))
            {
                return false;
            }

            run = candidate;
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
            ShowNext();
            return true;
        }

        public void Advance()
        {
            if (!IsTalking)
            {
                return;
            }

            dialogueBox.Confirm();
            if (!dialogueBox.IsOpen)
            {
                ShowNext();
            }
        }

        private void Awake()
        {
            services = FindAnyObjectByType<GameServices>();
            pause = FindAnyObjectByType<PauseMenuController>();
            confirmAction = sandbox.Controls.FindAction(BattleMenuInput.ConfirmAction, true);
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
        }

        private void OnPlayerCellEntered(Coord cell)
        {
            if (IsTalking || sandbox.StateMachine.Mode != GameMode.Exploration || !TryFindUnfiredTrigger(cell, out var trigger))
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
                if (sandbox.StateMachine.Mode == GameMode.Dialogue && confirmAction.WasPressedThisFrame() && Time.frameCount != openedFrame)
                {
                    Advance();
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
            Remember(new StoryEffect(trigger.Flag));
            if (!TryStartDialogue(trigger.Dialogue))
            {
                SaveProgress();
            }
        }

        private void ShowNext()
        {
            if (!run.TryAdvance(out var line))
            {
                Finish();
                return;
            }

            dialogueBox.Show(new DialogueLine(library.SpeakerName(line.SpeakerId), PortraitFor(line.SpeakerId), line.Text));
        }

        private void Finish()
        {
            sessionEffects.AddRange(run.AppliedEffects);
            run = null;
            closedFrame = Time.frameCount;

            var from = sandbox.StateMachine.Mode;
            sandbox.StateMachine.TryCloseOverlay();
            if (pause)
            {
                pause.Input.Panel = pause;
            }

            if (AutosavePolicy.ShouldSave(from, sandbox.StateMachine.Mode))
            {
                SaveProgress();
            }
        }

        private void Remember(StoryEffect effect)
        {
            if (Story != null && effect.TryApply(Story, out _))
            {
                sessionEffects.Add(effect);
            }
        }

        private void SaveProgress()
        {
            if (!AutosaveEnabled || !services || !services.IsBooted)
            {
                return;
            }

            var save = sandbox.CaptureSave();
            foreach (var effect in sessionEffects)
            {
                effect.TryApply(save, out _);
            }

            services.Autosave(save);
        }

        private bool CanTalkTo(NpcDialogueLink talker) =>
            talker.Actor && talker.Actor.Walker != null && library && library.Contains(talker.Dialogue);

        private NpcDialogueLink TalkerAt(Coord cell) =>
            talkers.Find(talker => CanTalkTo(talker) && talker.Actor.Walker.Cell == cell);

        private static bool IsBeside(Coord cell, NpcDialogueLink talker)
        {
            var npc = talker.Actor.Walker.Cell;
            return Math.Abs(cell.X - npc.X) + Math.Abs(cell.Y - npc.Y) == 1;
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

            trigger = triggers.Find(candidate => candidate.Cell == cell && !StoryCondition.Has(candidate.Flag).IsMet(Story));
            return trigger != null;
        }
    }
}
