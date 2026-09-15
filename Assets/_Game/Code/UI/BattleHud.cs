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
        [SerializeField] private BarView playerCoherence;
        [SerializeField] private BarView opponentCoherence;
        [SerializeField] private AudienceMeterView audience;
        [SerializeField] private ActionMenuView menu;
        [SerializeField] private DialogueBoxView dialogue;

        private RangeHighlighter previewHighlighter;

        public Canvas Canvas => canvas;

        public BarView PlayerCoherence => playerCoherence;

        public BarView OpponentCoherence => opponentCoherence;

        public AudienceMeterView Audience => audience;

        public ActionMenuView Menu => menu;

        public DialogueBoxView Dialogue => dialogue;

        public BattleState State { get; private set; }

        public void Configure(Canvas hudCanvas, BarView player, BarView opponent, AudienceMeterView audienceMeter, ActionMenuView actionMenu, DialogueBoxView dialogueBox)
        {
            canvas = hudCanvas;
            playerCoherence = player;
            opponentCoherence = opponent;
            audience = audienceMeter;
            menu = actionMenu;
            dialogue = dialogueBox;
        }

        public void Bind(BattleState state, BattleMenuController controller)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            playerCoherence.Bind(new DrainingBar(state.Player.MaxCoherence, state.Player.Coherence));
            opponentCoherence.Bind(new DrainingBar(state.Opponent.MaxCoherence, state.Opponent.Coherence));
            audience.Bind(new AudienceMeter(state.PlayerAudience));
            menu.Bind(controller);
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

            playerCoherence.Bar.SetValue(State.Player.Coherence);
            opponentCoherence.Bar.SetValue(State.Opponent.Coherence);
            audience.Meter.SetPlayerShare(State.PlayerAudience);
            menu.Controller?.Refresh();
            menu.Render();
        }

        private void OnDestroy()
        {
            if (menu)
            {
                menu.SelectionChanged -= OnSelectionChanged;
            }
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
