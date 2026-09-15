using Game.Grid;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Game.UI
{
    public sealed class RangeHighlightView : MonoBehaviour
    {
        [SerializeField] private Tilemap tilemap;
        [SerializeField] private TileBase moveTile;
        [SerializeField] private TileBase refuteTile;
        [SerializeField] private TileBase maieuticsTile;

        private RangeHighlighter highlighter;

        public int PaintedCount { get; private set; }

        public void Configure(Tilemap target, TileBase move, TileBase refute, TileBase maieutics)
        {
            tilemap = target;
            moveTile = move;
            refuteTile = refute;
            maieuticsTile = maieutics;
        }

        public void Bind(RangeHighlighter source)
        {
            if (highlighter != null)
            {
                highlighter.Changed -= Paint;
            }

            highlighter = source;
            if (highlighter != null)
            {
                highlighter.Changed += Paint;
            }

            Paint();
        }

        public TileBase TileFor(HighlightKind kind) => kind switch
        {
            HighlightKind.Move => moveTile,
            HighlightKind.Refute => refuteTile,
            HighlightKind.Maieutics => maieuticsTile,
            _ => null
        };

        private void OnDestroy() => Bind(null);

        private void Paint()
        {
            if (tilemap == null)
            {
                return;
            }

            tilemap.ClearAllTiles();
            PaintedCount = 0;
            var tile = highlighter != null ? TileFor(highlighter.Kind) : null;
            if (tile == null)
            {
                return;
            }

            foreach (var cell in highlighter.Cells)
            {
                tilemap.SetTile(new Vector3Int(cell.X, cell.Y, 0), tile);
                PaintedCount++;
            }
        }
    }
}
