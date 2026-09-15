using Game.Core;
using Game.Grid;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using TileData = Game.Grid.TileData;

namespace Game.Tests.EditMode.UI
{
    public sealed class RangeHighlightViewTests
    {
        private const int Size = 7;

        private GameObject host;
        private Tilemap tilemap;
        private RangeHighlightView view;
        private Tile move;
        private Tile refute;
        private Tile maieutics;
        private RangeHighlighter highlighter;

        [SetUp]
        public void CreateView()
        {
            host = new GameObject("grid", typeof(UnityEngine.Grid));
            var tilemapObject = new GameObject("range", typeof(Tilemap), typeof(TilemapRenderer));
            tilemapObject.transform.SetParent(host.transform, false);
            tilemap = tilemapObject.GetComponent<Tilemap>();

            move = ScriptableObject.CreateInstance<Tile>();
            refute = ScriptableObject.CreateInstance<Tile>();
            maieutics = ScriptableObject.CreateInstance<Tile>();
            view = tilemapObject.AddComponent<RangeHighlightView>();
            view.Configure(tilemap, move, refute, maieutics);

            var grid = new GridModel(Size, Size);
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    grid.TrySet(new Coord(x, y), new TileData { Cost = (byte)PathFinder.MinimumStepCost, Revealed = true });
                }
            }

            highlighter = new RangeHighlighter(grid, new PathFinder());
        }

        [TearDown]
        public void DestroyView()
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(move);
            Object.DestroyImmediate(refute);
            Object.DestroyImmediate(maieutics);
        }

        [Test]
        public void Bind_PaintsEveryHighlightedCellWithTheTileOfItsKind()
        {
            view.Bind(highlighter);
            highlighter.ShowRefute(new Coord(3, 3), 1);

            Assert.AreEqual(highlighter.Cells.Count, view.PaintedCount);
            foreach (var cell in highlighter.Cells)
            {
                Assert.AreSame(refute, tilemap.GetTile(new Vector3Int(cell.X, cell.Y, 0)));
            }
        }

        [Test]
        public void ChangingKind_RepaintsWithoutLeavingOldCells()
        {
            view.Bind(highlighter);
            highlighter.ShowMove(new Coord(3, 3), 3);
            highlighter.ShowMaieutics(new Coord(0, 0), 1);

            Assert.AreEqual(highlighter.Cells.Count, view.PaintedCount);
            Assert.IsNull(tilemap.GetTile(new Vector3Int(6, 3, 0)));
            Assert.AreSame(maieutics, tilemap.GetTile(new Vector3Int(1, 0, 0)));
        }

        [Test]
        public void ClearAndUnbind_EmptyTheTilemap()
        {
            view.Bind(highlighter);
            highlighter.ShowMove(new Coord(3, 3), 2);
            highlighter.Clear();
            Assert.AreEqual(0, view.PaintedCount);

            highlighter.ShowMove(new Coord(3, 3), 2);
            view.Bind(null);
            Assert.AreEqual(0, view.PaintedCount);
            Assert.IsNull(tilemap.GetTile(new Vector3Int(3, 3, 0)));

            highlighter.ShowRefute(new Coord(3, 3), 1);
            Assert.AreEqual(0, view.PaintedCount);
        }
    }
}
