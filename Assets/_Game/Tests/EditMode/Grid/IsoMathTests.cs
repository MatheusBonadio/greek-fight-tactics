using Game.Core;
using Game.Grid;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Grid
{
    public sealed class IsoMathTests
    {
        private const int RoundTripSamples = 10000;
        private const int Seed = 402;
        private const int CoordinateRange = 1000;

        [Test]
        public void WorldToGrid_InvertsGridToWorld_ForRandomCells()
        {
            var random = new System.Random(Seed);
            for (var sample = 0; sample < RoundTripSamples; sample++)
            {
                var cell = new Coord(
                    random.Next(-CoordinateRange, CoordinateRange),
                    random.Next(-CoordinateRange, CoordinateRange));

                Assert.AreEqual(cell, IsoMath.WorldToGrid(IsoMath.GridToWorld(cell)));
            }
        }

        [Test]
        public void GridToWorld_PlacesAxisNeighboursHalfATileAway()
        {
            Assert.AreEqual(new Vector2(0f, 0f), IsoMath.GridToWorld(new Coord(0, 0)));
            Assert.AreEqual(new Vector2(1f, 0.5f), IsoMath.GridToWorld(new Coord(1, 0)));
            Assert.AreEqual(new Vector2(-1f, 0.5f), IsoMath.GridToWorld(new Coord(0, 1)));
        }

        [TestCase(0.99f, 0f)]
        [TestCase(-0.99f, 0f)]
        [TestCase(0f, 0.49f)]
        [TestCase(0f, -0.49f)]
        [TestCase(0.49f, 0.24f)]
        [TestCase(-0.49f, -0.24f)]
        public void WorldToGrid_KeepsPointsInsideTheDiamond(float offsetX, float offsetY)
        {
            var cell = new Coord(7, -3);
            var world = IsoMath.GridToWorld(cell) + new Vector2(offsetX, offsetY);

            Assert.AreEqual(cell, IsoMath.WorldToGrid(world));
        }

        [TestCase(1.01f, 0f, 1, -1)]
        [TestCase(-1.01f, 0f, -1, 1)]
        [TestCase(0f, 0.51f, 1, 1)]
        [TestCase(0f, -0.51f, -1, -1)]
        [TestCase(0.51f, 0.26f, 1, 0)]
        [TestCase(-0.51f, 0.26f, 0, 1)]
        public void WorldToGrid_MovesPointsPastTheEdgeToTheNeighbour(float offsetX, float offsetY, int deltaX, int deltaY)
        {
            var cell = new Coord(7, -3);
            var world = IsoMath.GridToWorld(cell) + new Vector2(offsetX, offsetY);

            Assert.AreEqual(cell + new Coord(deltaX, deltaY), IsoMath.WorldToGrid(world));
        }

        [Test]
        public void SortingOrder_DrawsCellsFartherBackFirst()
        {
            var front = IsoMath.SortingOrder(new Coord(0, 0));
            var back = IsoMath.SortingOrder(new Coord(1, 1));

            Assert.Greater(front, back);
            Assert.AreEqual(IsoMath.SortingStep * 2, front - back);
        }
    }
}
