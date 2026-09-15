using Game.Battle;
using Game.Core;
using Game.Grid;

namespace Game.UI
{
    public static class CirculateTargeting
    {
        public static bool IsValidDestination(RangeHighlighter highlighter, BattleState state, Coord cell) =>
            highlighter != null
            && state != null
            && highlighter.Kind == HighlightKind.Move
            && highlighter.Contains(cell)
            && cell != state.Player.Position;
    }
}
