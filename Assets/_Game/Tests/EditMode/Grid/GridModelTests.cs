using System;
using Game.Core;
using Game.Grid;
using NUnit.Framework;

namespace Game.Tests.EditMode.Grid
{
    public sealed class GridModelTests
    {
        private const int Width = 12;
        private const int Height = 8;

        private static readonly Coord[] OutsideCells =
        {
            new(-1, 0),
            new(0, -1),
            new(Width, 0),
            new(0, Height),
            new(int.MinValue, int.MinValue),
            new(int.MaxValue, int.MaxValue)
        };

        [Test]
        public void TrySet_ThenTryGet_ReturnsTheStoredTile()
        {
            var grid = new GridModel(Width, Height);
            var cell = new Coord(Width - 1, Height - 1);
            var tile = new TileData { Terrain = TerrainId.Stone, Cost = 3, Revealed = true, OccupantId = 42 };

            Assert.IsTrue(grid.TrySet(cell, tile));
            Assert.IsTrue(grid.TryGet(cell, out var stored));
            Assert.AreEqual(tile, stored);
            Assert.AreEqual(tile, grid[cell]);
        }

        [TestCaseSource(nameof(OutsideCells))]
        public void OutsideCells_AreReportedInvalid_WithoutThrowing(Coord cell)
        {
            var grid = new GridModel(Width, Height);

            Assert.IsFalse(grid.Contains(cell));
            Assert.IsFalse(grid.TryGet(cell, out var tile));
            Assert.AreEqual(TileData.OutOfBounds, tile);
            Assert.AreEqual(TileData.OutOfBounds, grid[cell]);
            Assert.IsFalse(grid.TrySet(cell, new TileData()));
        }

        [Test]
        public void OutOfBoundsTile_IsImpassableAndEmpty()
        {
            Assert.IsFalse(TileData.OutOfBounds.IsPassable);
            Assert.IsFalse(TileData.OutOfBounds.IsOccupied);
        }

        [TestCase(0, Height)]
        [TestCase(Width, 0)]
        [TestCase(-1, Height)]
        public void Constructor_RejectsNonPositiveSizes(int width, int height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridModel(width, height));
        }
    }
}
