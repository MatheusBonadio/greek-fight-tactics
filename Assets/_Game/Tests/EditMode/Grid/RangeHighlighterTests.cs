using System.Collections.Generic;
using Game.Core;
using Game.Grid;
using NUnit.Framework;

namespace Game.Tests.EditMode.Grid
{
    public sealed class RangeHighlighterTests
    {
        private static readonly Coord Center = new(4, 4);

        private GridModel grid;
        private RangeHighlighter highlighter;

        [SetUp]
        public void SetUp()
        {
            grid = GridFactory.Open(9, 9);
            new FogOfWar(grid).Reveal(Center, 8);
            highlighter = new RangeHighlighter(grid, new PathFinder());
        }

        [Test]
        public void ShowMove_UsesThePathFinderRange()
        {
            GridFactory.Block(grid, new Coord(5, 4));

            highlighter.ShowMove(Center, 2);

            Assert.AreEqual(HighlightKind.Move, highlighter.Kind);
            CollectionAssert.DoesNotContain(highlighter.Cells, new Coord(5, 4));
            CollectionAssert.DoesNotContain(highlighter.Cells, new Coord(6, 4));
            CollectionAssert.Contains(highlighter.Cells, new Coord(3, 4));
            CollectionAssert.DoesNotContain(highlighter.Cells, Center);
        }

        [TestCase(HighlightKind.Refute)]
        [TestCase(HighlightKind.Maieutics)]
        public void ShowDiamond_IgnoresTerrainAndOccupants(HighlightKind kind)
        {
            GridFactory.Block(grid, new Coord(5, 4));
            GridFactory.Occupy(grid, new Coord(4, 5), 9);

            Show(kind, 2);

            Assert.AreEqual(kind, highlighter.Kind);
            Assert.AreEqual(12, highlighter.Cells.Count);
            CollectionAssert.Contains(highlighter.Cells, new Coord(5, 4));
            CollectionAssert.Contains(highlighter.Cells, new Coord(6, 4));
            CollectionAssert.Contains(highlighter.Cells, new Coord(4, 5));
        }

        [Test]
        public void SwitchingAction_ReplacesThePreviousHighlight()
        {
            var changes = 0;
            highlighter.Changed += () => changes++;

            highlighter.ShowMove(Center, 3);
            var moveCells = new List<Coord>(highlighter.Cells);
            highlighter.ShowRefute(Center, 1);

            Assert.AreEqual(HighlightKind.Refute, highlighter.Kind);
            Assert.AreEqual(4, highlighter.Cells.Count);
            Assert.Greater(moveCells.Count, highlighter.Cells.Count);
            Assert.AreEqual(2, changes);

            highlighter.Clear();

            Assert.AreEqual(HighlightKind.None, highlighter.Kind);
            Assert.IsEmpty(highlighter.Cells);
            Assert.AreEqual(3, changes);
        }

        [TestCase(HighlightKind.Move)]
        [TestCase(HighlightKind.Refute)]
        [TestCase(HighlightKind.Maieutics)]
        public void NoHighlight_IsEverDrawnOverFog(HighlightKind kind)
        {
            var foggy = GridFactory.Open(9, 9);
            new FogOfWar(foggy).Reveal(Center, 1);
            var foggyHighlighter = new RangeHighlighter(foggy, new PathFinder());

            switch (kind)
            {
                case HighlightKind.Move:
                    foggyHighlighter.ShowMove(Center, 4);
                    break;
                case HighlightKind.Refute:
                    foggyHighlighter.ShowRefute(Center, 4);
                    break;
                default:
                    foggyHighlighter.ShowMaieutics(Center, 4);
                    break;
            }

            Assert.AreEqual(4, foggyHighlighter.Cells.Count);
            foreach (var cell in foggyHighlighter.Cells)
            {
                Assert.IsTrue(foggy[cell].Revealed, $"{kind} highlighted fogged cell {cell}");
            }
        }

        private void Show(HighlightKind kind, int radius)
        {
            if (kind == HighlightKind.Refute)
            {
                highlighter.ShowRefute(Center, radius);
                return;
            }

            highlighter.ShowMaieutics(Center, radius);
        }
    }
}
