using System;
using Game.Core;

namespace Game.Grid
{
    public static class FacingRules
    {
        public static Facing FromStep(Coord from, Coord to, Facing current)
        {
            var deltaX = to.X - from.X;
            var deltaY = to.Y - from.Y;

            if (deltaX > 0 && deltaY == 0)
            {
                return Facing.NorthEast;
            }

            if (deltaX < 0 && deltaY == 0)
            {
                return Facing.SouthWest;
            }

            if (deltaY > 0 && deltaX == 0)
            {
                return Facing.NorthWest;
            }

            if (deltaY < 0 && deltaX == 0)
            {
                return Facing.SouthEast;
            }

            return current;
        }

        public static bool IsMirrored(Facing facing) => facing is Facing.SouthWest or Facing.NorthWest;

        public static Facing SheetFacing(Facing facing) => facing switch
        {
            Facing.SouthWest => Facing.SouthEast,
            Facing.NorthWest => Facing.NorthEast,
            Facing.SouthEast => Facing.SouthEast,
            Facing.NorthEast => Facing.NorthEast,
            _ => throw new ArgumentOutOfRangeException(nameof(facing), facing, null)
        };
    }
}
