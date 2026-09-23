using System;
using Game.Core;
using UnityEngine;

namespace Game.Grid
{
    public static class TerrainSorting
    {
        public const int MaxLevel = 3;
        public const float LevelStep = 0.07f;
        public const float StepSlack = 0.015f;
        public const float TileSortPoint = -IsoMath.CellHeight;

        public const float FloorOffset = 0.06f;
        public const float FaceOffset = 0.06f;
        public const float FaceVeilOffset = 0.058f;
        public const float ExitOffset = 0.055f;
        public const float FloorVeilOffset = 0.05f;
        public const float RangeOffset = 0.04f;
        public const float HighlightOffset = 0.03f;
        public const float FogOffset = 0.02f;
        public const float ShadowOffset = 0.01f;
        public const float BlockOffset = -0.25f;
        public const float BlockVeilOffset = -0.26f;

        public static float SortValue(Coord cell, float level, float offset) =>
            IsoMath.GridToWorld(cell).y + offset - LevelStep * level;

        public static float TilemapLift(int level, float drop = 0f) => IsoMath.Lift(level) + drop;

        public static float TilemapDepth(int level, float offset, float drop = 0f) =>
            TilemapLift(level, drop) + TileSortPoint - offset + LevelStep * level;

        public static float TileSortValue(Coord cell, int level, float offset, float drop = 0f) =>
            IsoMath.GridToWorld(cell).y + TileSortPoint + TilemapLift(level, drop) - TilemapDepth(level, offset, drop);

        public static float ActorSortValue(Vector2 ground, float level, int frontDepth)
        {
            var front = frontDepth * IsoMath.CellHeight;
            return front + Math.Min(ground.y - front, StepSlack) - LevelStep * level;
        }

        public static float ActorDepth(Vector2 ground, float level, int frontDepth) =>
            ground.y + IsoMath.Lift(level) - ActorSortValue(ground, level, frontDepth);
    }
}
