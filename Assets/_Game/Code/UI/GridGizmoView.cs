using Game.Core;
using Game.Grid;
using UnityEngine;

namespace Game.UI
{
    public sealed class GridGizmoView : MonoBehaviour
    {
        [SerializeField] private int width = 12;
        [SerializeField] private int height = 12;
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private Color lineColor = new(0.28f, 0.78f, 0.88f, 0.6f);

        public GridModel Model { get; set; }

        public bool DrawGizmos
        {
            get => drawGizmos;
            set => drawGizmos = value;
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos)
            {
                return;
            }

            var columns = Model?.Width ?? width;
            var rows = Model?.Height ?? height;
            var origin = (Vector2)transform.position;
            Gizmos.color = lineColor;

            for (var x = 0; x < columns; x++)
            {
                for (var y = 0; y < rows; y++)
                {
                    var cell = new Coord(x, y);
                    DrawDiamond(origin + IsoMath.GridToSurface(cell, Model?[cell].Level ?? 0));
                }
            }
        }

        private static void DrawDiamond(Vector2 center)
        {
            var right = center + new Vector2(IsoMath.CellWidth, 0f);
            var top = center + new Vector2(0f, IsoMath.CellHeight);
            var left = center - new Vector2(IsoMath.CellWidth, 0f);
            var bottom = center - new Vector2(0f, IsoMath.CellHeight);

            Gizmos.DrawLine(right, top);
            Gizmos.DrawLine(top, left);
            Gizmos.DrawLine(left, bottom);
            Gizmos.DrawLine(bottom, right);
        }
    }
}
