using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Game.UI
{
    public sealed class LevelTilemaps : MonoBehaviour
    {
        [SerializeField] private int width;
        [SerializeField] private byte[] cellLevels = new byte[0];
        [SerializeField] private int lowestLevel;
        [SerializeField] private Tilemap[] tilemaps = new Tilemap[0];

        public IReadOnlyList<Tilemap> Tilemaps => tilemaps;

        public int LowestLevel => lowestLevel;

        public int HighestLevel => lowestLevel + tilemaps.Length - 1;

        public void Configure(int mapWidth, byte[] levels, int lowest, Tilemap[] byLevel)
        {
            width = mapWidth;
            cellLevels = levels ?? new byte[0];
            lowestLevel = lowest;
            tilemaps = byLevel ?? new Tilemap[0];
        }

        public Tilemap At(int level)
        {
            var index = level - lowestLevel;
            return index >= 0 && index < tilemaps.Length ? tilemaps[index] : null;
        }

        public int LevelOf(Vector3Int cell)
        {
            var index = cell.y * width + cell.x;
            return cell.x >= 0 && cell.x < width && index >= 0 && index < cellLevels.Length ? cellLevels[index] : 0;
        }

        public Tilemap For(Vector3Int cell) => At(LevelOf(cell));

        public void SetTile(Vector3Int cell, TileBase tile) => SetTile(cell, LevelOf(cell), tile);

        public void SetTile(Vector3Int cell, int level, TileBase tile)
        {
            var tilemap = At(level);
            if (tilemap && tilemap.GetTile(cell) != tile)
            {
                tilemap.SetTile(cell, tile);
            }
        }

        public TileBase GetTile(Vector3Int cell) => GetTile(cell, LevelOf(cell));

        public TileBase GetTile(Vector3Int cell, int level)
        {
            var tilemap = At(level);
            return tilemap ? tilemap.GetTile(cell) : null;
        }

        public bool HasTile(Vector3Int cell) => GetTile(cell) != null;

        public void ClearAllTiles()
        {
            foreach (var tilemap in tilemaps.Where(tilemap => tilemap))
            {
                tilemap.ClearAllTiles();
            }
        }

        public bool Contains(Tilemap tilemap) => tilemap && tilemaps.Contains(tilemap);

        public int LevelOf(Tilemap tilemap)
        {
            var index = System.Array.IndexOf(tilemaps, tilemap);
            return index < 0 ? lowestLevel - 1 : lowestLevel + index;
        }

        public int CountTiles()
        {
            var count = 0;
            foreach (var tilemap in tilemaps.Where(tilemap => tilemap))
            {
                foreach (var position in tilemap.cellBounds.allPositionsWithin)
                {
                    count += tilemap.HasTile(position) ? 1 : 0;
                }
            }

            return count;
        }

        public IEnumerable<string> UsedTileNames()
        {
            foreach (var tilemap in tilemaps.Where(tilemap => tilemap))
            {
                var used = new TileBase[tilemap.GetUsedTilesCount()];
                tilemap.GetUsedTilesNonAlloc(used);
                foreach (var tile in used.Where(tile => tile))
                {
                    yield return tile.name;
                }
            }
        }
    }
}
