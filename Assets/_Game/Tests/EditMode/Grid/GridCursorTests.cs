using System.Collections.Generic;
using Game.Core;
using Game.Grid;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Grid
{
    public sealed class GridCursorTests
    {
        private static readonly Vector2 Origin = new(10f, -4f);

        private GridModel grid;
        private GridCursor cursor;

        [SetUp]
        public void SetUp()
        {
            grid = GridFactory.Open(12, 12);
            cursor = new GridCursor(grid, new Coord(5, 5));
        }

        [TestCase(0f, 0f, 7, 3)]
        [TestCase(0.9f, 0f, 7, 3)]
        [TestCase(0f, 0.45f, 7, 3)]
        [TestCase(1.1f, 0f, 8, 2)]
        public void Hover_ConvertsThePointerPositionIntoACell(float offsetX, float offsetY, int expectedX, int expectedY)
        {
            var world = Origin + IsoMath.GridToWorld(new Coord(7, 3)) + new Vector2(offsetX, offsetY);

            Assert.IsTrue(cursor.TryHover(world, Origin));
            Assert.AreEqual(new Coord(expectedX, expectedY), cursor.Cell);
        }

        [Test]
        public void Hover_OutsideTheGrid_KeepsTheCursorWhereItWas()
        {
            Assert.IsFalse(cursor.TryHover(Origin + IsoMath.GridToWorld(new Coord(-3, 20)), Origin));
            Assert.AreEqual(new Coord(5, 5), cursor.Cell);
        }

        [TestCase(0f, 1f, 1, 1)]
        [TestCase(0f, -1f, -1, -1)]
        [TestCase(1f, 0f, 1, -1)]
        [TestCase(-1f, 0f, -1, 1)]
        [TestCase(1f, 1f, 1, 0)]
        [TestCase(-1f, 1f, 0, 1)]
        public void Step_MovesAlongTheScreenDirection(float right, float up, int deltaX, int deltaY)
        {
            var start = IsoMath.GridToWorld(cursor.Cell);

            Assert.IsTrue(cursor.TryStep(new Vector2(right, up)));

            Assert.AreEqual(new Coord(5 + deltaX, 5 + deltaY), cursor.Cell);
            var screenStep = IsoMath.GridToWorld(cursor.Cell) - start;
            if (right != 0f)
            {
                Assert.AreEqual(Mathf.Sign(right), Mathf.Sign(screenStep.x));
            }

            if (up != 0f)
            {
                Assert.AreEqual(Mathf.Sign(up), Mathf.Sign(screenStep.y));
            }
        }

        [Test]
        public void Step_InsideTheDeadZone_DoesNothing()
        {
            Assert.IsFalse(cursor.TryStep(new Vector2(0.3f, -0.4f)));
            Assert.AreEqual(new Coord(5, 5), cursor.Cell);
        }

        [Test]
        public void Hover_OverACellAnObjectFills_KeepsTheCursorWhereItWas()
        {
            var blocked = new Coord(7, 3);
            GridFactory.Block(grid, blocked);

            Assert.IsFalse(cursor.TryHover(Origin + IsoMath.GridToWorld(blocked), Origin));
            Assert.AreEqual(new Coord(5, 5), cursor.Cell);
        }

        [Test]
        public void Step_IntoACellAnObjectFills_KeepsTheCursorWhereItWas()
        {
            GridFactory.Block(grid, new Coord(6, 6));
            var moves = new List<Coord>();
            cursor.Moved += moves.Add;

            Assert.IsFalse(cursor.TryStep(new Vector2(0f, 1f)));
            Assert.AreEqual(new Coord(5, 5), cursor.Cell);
            CollectionAssert.IsEmpty(moves);
        }

        [Test]
        public void Hover_OverAnActor_StillReachesTheCellSoSheCanBeTalkedTo()
        {
            var npc = new Coord(7, 3);
            GridFactory.Occupy(grid, npc, 42);

            Assert.IsTrue(cursor.TryHover(Origin + IsoMath.GridToWorld(npc), Origin));
            Assert.AreEqual(npc, cursor.Cell);
        }

        [Test]
        public void Point_StillReachesABlockedCellBecauseItAnswersToCodeAndNotToThePlayer()
        {
            var blocked = new Coord(7, 3);
            GridFactory.Block(grid, blocked);

            Assert.IsTrue(cursor.TryPoint(blocked));
            Assert.AreEqual(blocked, cursor.Cell);
        }

        [Test]
        public void Step_AtTheEdge_StaysInsideAndRaisesMovedOnlyOnChange()
        {
            var edge = new GridCursor(grid, new Coord(11, 11));
            var moves = new List<Coord>();
            edge.Moved += moves.Add;

            Assert.IsFalse(edge.TryStep(new Vector2(0f, 1f)));
            Assert.IsTrue(edge.TryStep(new Vector2(0f, -1f)));
            Assert.IsTrue(edge.TryHover(IsoMath.GridToWorld(new Coord(10, 10)), Vector2.zero));

            CollectionAssert.AreEqual(new[] { new Coord(10, 10) }, moves);
        }

        [TestCase(0.0, 0)]
        [TestCase(0.49, 0)]
        [TestCase(0.5, 1)]
        [TestCase(0.99, 1)]
        [TestCase(1.0, 0)]
        [TestCase(2.75, 1)]
        public void SelectionPulse_AlternatesEveryHalfSecond(double seconds, int expectedFrame)
        {
            Assert.AreEqual(expectedFrame, SelectionPulse.FrameAt(seconds));
        }

        [TestCase(0.75, 0.75, 0)]
        [TestCase(0.75, 1.2, 0)]
        [TestCase(0.75, 1.3, 1)]
        [TestCase(0.75, 1.8, 0)]
        public void SelectionPulse_RestartsVisibleWhenTheCursorMoves(double movedAt, double now, int expectedFrame)
        {
            Assert.AreEqual(1, SelectionPulse.FrameAt(movedAt), "the global clock is in the hidden frame when the cursor moves");
            Assert.AreEqual(expectedFrame, SelectionPulse.FrameSince(now, movedAt));
        }
    }
}
