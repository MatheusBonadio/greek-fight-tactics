using System;
using System.Collections.Generic;

namespace Game.Core
{
    public sealed class GameStateMachine
    {
        private readonly Stack<GameMode> suspended = new();

        public GameStateMachine(GameMode initial = GameMode.Exploration)
        {
            Mode = initial;
        }

        public event Action<GameMode, GameMode> Changed;

        public GameMode Mode { get; private set; }

        public bool IsInBattle => Mode is GameMode.BattleIntro or GameMode.BattleTurn or GameMode.BattleResolve or GameMode.BattleOutro;

        public bool IsExplorationInputEnabled => Mode == GameMode.Exploration;

        public bool IsBattleInputEnabled => Mode == GameMode.BattleTurn;

        public static bool CanTransition(GameMode from, GameMode to) => (from, to) switch
        {
            (GameMode.Exploration, GameMode.BattleIntro) => true,
            (GameMode.BattleIntro, GameMode.BattleTurn) => true,
            (GameMode.BattleTurn, GameMode.BattleResolve) => true,
            (GameMode.BattleResolve, GameMode.BattleTurn) => true,
            (GameMode.BattleResolve, GameMode.BattleOutro) => true,
            (GameMode.BattleOutro, GameMode.Exploration) => true,
            _ => false
        };

        public bool TryTransition(GameMode to)
        {
            if (!CanTransition(Mode, to))
            {
                return false;
            }

            Change(to);
            return true;
        }

        public bool TryOpenOverlay(GameMode overlay)
        {
            var isOverlay = overlay is GameMode.Dialogue or GameMode.Paused or GameMode.Cutscene;
            var alreadyPaused = Mode == GameMode.Paused;
            if (!isOverlay || alreadyPaused || Mode == overlay)
            {
                return false;
            }

            suspended.Push(Mode);
            Change(overlay);
            return true;
        }

        public bool TryCloseOverlay()
        {
            if (suspended.Count == 0)
            {
                return false;
            }

            Change(suspended.Pop());
            return true;
        }

        private void Change(GameMode to)
        {
            var from = Mode;
            Mode = to;
            Changed?.Invoke(from, to);
        }
    }
}
