using System;
using Game.Battle;
using Game.Core;
using Game.Grid;

namespace Game.UI
{
    public static class BattleTargetPreview
    {
        public static HighlightKind Apply(RangeHighlighter highlighter, BattleState state, ActionMenuEntry entry)
        {
            if (highlighter == null)
            {
                throw new ArgumentNullException(nameof(highlighter));
            }

            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (!entry.IsAvailable)
            {
                highlighter.Clear();
                return highlighter.Kind;
            }

            var from = state.Player.Position;
            switch (entry.Action)
            {
                case ActionId.Circulate:
                    highlighter.ShowMove(from, state.Rules.MoveRange);
                    break;
                case ActionId.Refute:
                    highlighter.ShowRefute(from, state.Rules.RefuteRange);
                    break;
                case ActionId.Maieutics:
                    highlighter.ShowMaieutics(from, BattleResolver.MaieuticsRange(state));
                    break;
                default:
                    highlighter.Clear();
                    break;
            }

            return highlighter.Kind;
        }
    }
}
