using System;
using Game.Core;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Game.UI
{
    [Serializable]
    public sealed class FaceTile
    {
        [SerializeField] private Vector2Int cell;
        [SerializeField] private int level;
        [SerializeField] private TileBase tile;

        public FaceTile()
        {
        }

        public FaceTile(Coord faceCell, int faceLevel, TileBase faceTile)
        {
            cell = new Vector2Int(faceCell.X, faceCell.Y);
            level = faceLevel;
            tile = faceTile;
        }

        public Coord Cell => new(cell.x, cell.y);

        public Vector3Int Position => new(cell.x, cell.y, 0);

        public int Level => level;

        public TileBase Tile => tile;
    }
}
