using Game.Core;
using Game.Grid;
using NUnit.Framework;

namespace Game.Tests.EditMode.Grid
{
    public sealed class FogOfWarTests
    {
        private const int Seed = 402;
        private const int MaxBitmaskBytes = 1024;

        [Test]
        public void Reveal_WithDefaultRadius_UncoversTheDiamondAroundTheCenter()
        {
            var grid = GridFactory.Open(9, 9);
            var fog = new FogOfWar(grid);
            var center = new Coord(4, 4);

            Assert.AreEqual(25, fog.Reveal(center));

            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var distance = System.Math.Abs(x - center.X) + System.Math.Abs(y - center.Y);
                    Assert.AreEqual(distance <= FogOfWar.DefaultRevealRadius, grid[new Coord(x, y)].Revealed);
                }
            }
        }

        [Test]
        public void Reveal_NearTheCorner_ClipsToTheGrid()
        {
            var grid = GridFactory.Open(4, 4);
            var fog = new FogOfWar(grid);

            Assert.AreEqual(10, fog.Reveal(new Coord(0, 0)));
        }

        [Test]
        public void Reveal_IsPermanent_AndCountsOnlyNewCells()
        {
            var grid = GridFactory.Open(12, 12);
            var fog = new FogOfWar(grid);

            fog.Reveal(new Coord(5, 5));
            var overlap = fog.Reveal(new Coord(6, 5));

            Assert.AreEqual(7, overlap);
            Assert.IsTrue(grid[new Coord(2, 5)].Revealed);
        }

        [Test]
        public void Bitmask_RoundTrip_RestoresIdenticalFog()
        {
            var original = GridFactory.Open(37, 23);
            var fog = new FogOfWar(original);
            var random = new System.Random(Seed);
            for (var reveal = 0; reveal < 15; reveal++)
            {
                fog.Reveal(new Coord(random.Next(original.Width), random.Next(original.Height)), random.Next(0, 5));
            }

            var restored = GridFactory.Open(original.Width, original.Height);
            Assert.IsTrue(new FogOfWar(restored).TryLoadBitmask(fog.ToBitmask()));

            for (var y = 0; y < original.Height; y++)
            {
                for (var x = 0; x < original.Width; x++)
                {
                    var cell = new Coord(x, y);
                    Assert.AreEqual(original[cell].Revealed, restored[cell].Revealed, $"cell {cell}");
                }
            }
        }

        [Test]
        public void Bitmask_ForLargestMap_FitsInOneKilobyte()
        {
            var fog = new FogOfWar(GridFactory.Open(64, 64));

            Assert.AreEqual(512, fog.ToBitmask().Length);
            Assert.Less(fog.ToBitmask().Length, MaxBitmaskBytes);
        }

        [Test]
        public void TryLoadBitmask_WithWrongLength_LeavesFogUntouched()
        {
            var grid = GridFactory.Open(8, 8);
            var fog = new FogOfWar(grid);
            fog.Reveal(new Coord(4, 4), 1);

            Assert.IsFalse(fog.TryLoadBitmask(new byte[fog.BitmaskLength + 1]));
            Assert.IsFalse(fog.TryLoadBitmask(null));
            Assert.IsTrue(grid[new Coord(4, 4)].Revealed);
        }
    }
}
