using System;
using Game.Battle;
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

        private void Awake()
        {
            if (canvas && !canvas.worldCamera)
            {
                canvas.worldCamera = Camera.main;
            }
        }
    }
}
