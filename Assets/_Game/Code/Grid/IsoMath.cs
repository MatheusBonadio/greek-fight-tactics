using System;
using Game.Core;
using UnityEngine;

namespace Game.Grid
{
    public static class IsoMath
    {
        public const float CellWidth = 1.0f;
        public const float CellHeight = 0.5f;
        public const int SortingStep = 10;
        public const int LevelPixels = 8;
        public const float LevelHeight = 0.5f;

        public static Vector2 GridToWorld(Coord cell) =>
            new((cell.X - cell.Y) * CellWidth, (cell.X + cell.Y) * CellHeight);

        public static Vector2 GridToSurface(Coord cell, float level) => GridToWorld(cell) + new Vector2(0f, Lift(level));

        public static float Lift(float level) => level * LevelHeight;

        public static Coord WorldToGrid(Vector2 world)
        {
            var alongX = (world.x / CellWidth + world.y / CellHeight) * 0.5f;
            var alongY = (world.y / CellHeight - world.x / CellWidth) * 0.5f;
            return new Coord(RoundHalfUp(alongX), RoundHalfUp(alongY));
        }

        public static int SortingOrder(Coord cell) => -(cell.X + cell.Y) * SortingStep;

        public static int Depth(Coord cell) => cell.X + cell.Y;

        private static int RoundHalfUp(float value) => (int)MathF.Floor(value + 0.5f);
    }
}
