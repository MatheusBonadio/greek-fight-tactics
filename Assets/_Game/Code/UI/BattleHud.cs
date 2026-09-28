using System;
using Game.Battle;
using Game.Core;
using Game.Grid;
using UnityEngine;

namespace Game.UI
{
    public sealed class BattleHud : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private DebaterPanelView playerPanel;
        [SerializeField] private DebaterPanelView opponentPanel;
        [SerializeField] private AudienceMeterView audience;
        [SerializeField] private ActionMenuView menu;
        [SerializeField] private DialogueBoxView dialogue;
        [SerializeField] private RectTransform dialogueArea;
        [SerializeField] private float dialogueLeft;
        [SerializeField] private float dialogueLeftBesideSubmenu;

        private RangeHighlighter previewHighlighter;

        public Canvas Canvas => canvas;

        public DebaterPanelView PlayerPanel => playerPanel;

        public DebaterPanelView OpponentPanel => opponentPanel;

        public BarView PlayerCoherence => playerPanel.Coherence;

        public BarView OpponentCoherence => opponentPanel.Coherence;

        public AudienceMeterView Audience => audience;

        public ActionMenuView Menu => menu;

        public DialogueBoxView Dialogue => dialogue;

        public RectTransform DialogueArea => dialogueArea;

        public float DialogueLeft => dialogueLeft;

        public float DialogueLeftBesideSubmenu => dialogueLeftBesideSubmenu;

        public BattleState State { get; private set; }

        public void Configure(Canvas hudCanvas, DebaterPanelView player, DebaterPanelView opponent, AudienceMeterView audienceMeter, ActionMenuView actionMenu, DialogueBoxView dialogueBox)
        {
            canvas = hudCanvas;
            playerPanel = player;
            opponentPanel = opponent;
            audience = audienceMeter;
            menu = actionMenu;
            dialogue = dialogueBox;
        }

        public void ConfigureDialogueArea(RectTransform area, float left, float leftBesideSubmenu)
        {
            dialogueArea = area;
            dialogueLeft = left;
            dialogueLeftBesideSubmenu = leftBesideSubmenu;
        }

        public void Bind(BattleState state, BattleMenuController controller)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            PlayerCoherence.Bind(new DrainingBar(state.Player.MaxCoherence, state.Player.Coherence));
            OpponentCoherence.Bind(new DrainingBar(state.Opponent.MaxCoherence, state.Opponent.Coherence));
            playerPanel.RenderValue();
            opponentPanel.RenderValue();
            audience.Bind(new AudienceMeter(state.PlayerAudience));
            ShowMorale();
            menu.SubmenuToggled -= PlaceDialogue;
            menu.SubmenuToggled += PlaceDialogue;
            menu.Bind(controller);
            PlaceDialogue(menu.SubmenuPanel && menu.SubmenuPanel.activeSelf);
        }

        public void Introduce(DebaterBadge player, DebaterBadge opponent)
        {
            playerPanel.Show(player);
            opponentPanel.Show(opponent);
        }

        public void BindPreview(RangeHighlighter highlighter)
        {
            menu.SelectionChanged -= OnSelectionChanged;
            previewHighlighter = highlighter;
            if (previewHighlighter == null)
            {
                return;
            }

            menu.SelectionChanged += OnSelectionChanged;
            if (State != null && menu.Controller != null)
            {
                BattleTargetPreview.Apply(previewHighlighter, State, menu.Controller.Actions.Selected);
            }
        }

        public void ApplySettings(GameSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            dialogue.CharactersPerSecond = settings.CharactersPerSecond;
            menu.AssistedMode = settings.AssistedMode;
        }

        public void Sync()
        {
            if (State == null)
            {
                return;
            }

            PlayerCoherence.Bar.SetValue(State.Player.Coherence);
            OpponentCoherence.Bar.SetValue(State.Opponent.Coherence);
            audience.Meter.SetPlayerShare(State.PlayerAudience);
            ShowMorale();
            menu.Controller?.Refresh();
            menu.Render();
        }

        private void ShowMorale()
        {
            playerPanel.ShowMorale(MoraleRule.ShiftFor(State, State.Player));
            opponentPanel.ShowMorale(MoraleRule.ShiftFor(State, State.Opponent));
        }

        private void OnDestroy()
        {
            if (menu)
            {
                menu.SelectionChanged -= OnSelectionChanged;
                menu.SubmenuToggled -= PlaceDialogue;
            }
        }

        private void PlaceDialogue(bool besideSubmenu)
        {
            if (!dialogueArea)
            {
                return;
            }

            dialogueArea.offsetMin = new Vector2(besideSubmenu ? dialogueLeftBesideSubmenu : dialogueLeft, dialogueArea.offsetMin.y);
        }

        private void OnSelectionChanged(ActionMenuEntry entry)
        {
            if (previewHighlighter != null && State != null)
            {
                BattleTargetPreview.Apply(previewHighlighter, State, entry);
            }
        }
    }
}
