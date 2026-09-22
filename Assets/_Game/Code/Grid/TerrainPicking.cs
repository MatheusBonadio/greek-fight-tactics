using Game.Core;
using UnityEngine;

namespace Game.Grid
{
    public static class TerrainPicking
    {
        public static Coord CellAt(GridModel grid, Vector2 world)
        {
            for (var pixel = grid.MaxLevel * IsoMath.LevelPixels; pixel > 0; pixel--)
            {
                var level = (float)pixel / IsoMath.LevelPixels;
                var cell = IsoMath.WorldToGrid(world - new Vector2(0f, IsoMath.Lift(level)));
                if (grid.TryGet(cell, out var tile) && tile.Level * IsoMath.LevelPixels >= pixel)
                {
                    return cell;
                }
            }

            return IsoMath.WorldToGrid(world);
        }
    }
}
