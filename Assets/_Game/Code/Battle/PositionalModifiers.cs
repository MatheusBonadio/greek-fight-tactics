using Game.Core;
using Game.Grid;

namespace Game.Battle
{
    public static class PositionalModifiers
    {
        private static readonly Coord East = new(1, 0);
        private static readonly Coord West = new(-1, 0);
        private static readonly Coord North = new(0, 1);
        private static readonly Coord South = new(0, -1);

        public static int AdjacentAudienceMembers(BattleState state, Coord cell)
        {
            var count = 0;
            foreach (var neighbour in Neighbours(cell))
            {
                if (state.IsAudienceMember(state.Grid[neighbour].OccupantId))
                {
                    count++;
                }
            }

            return count;
        }

        public static int BlockedNeighbours(BattleState state, Coord cell)
        {
            var blocked = 0;
            foreach (var neighbour in Neighbours(cell))
            {
                if (!state.Grid.TryGet(neighbour, out var tile) || !tile.IsPassable || tile.IsOccupied)
                {
                    blocked++;
                }
            }

            return blocked;
        }

        public static bool IsCornered(BattleState state, Coord cell) =>
            BlockedNeighbours(state, cell) >= state.Rules.CorneredBlockedNeighbours;

        public static bool IsFlanking(Debater attacker, Debater target)
        {
            var facing = FacingVector(target.Facing);
            var offsetX = attacker.Position.X - target.Position.X;
            var offsetY = attacker.Position.Y - target.Position.Y;
            return facing.X * offsetX + facing.Y * offsetY < 0;
        }

        public static bool StandsOnDiagram(BattleState state, Coord cell) =>
            state.Grid.TryGet(cell, out var tile) && tile.Terrain == TerrainId.Diagram;

        public static int Distance(Coord from, Coord to) =>
            System.Math.Abs(from.X - to.X) + System.Math.Abs(from.Y - to.Y);

        public static Coord FacingVector(Facing facing) => facing switch
        {
            Facing.NorthEast => East,
            Facing.SouthWest => West,
            Facing.NorthWest => North,
            _ => South
        };

        private static Coord[] Neighbours(Coord cell) => new[] { cell + East, cell + West, cell + North, cell + South };
    }
}
