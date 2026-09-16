using System.IO;
using System.Linq;
using Game.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Art
{
    public sealed class BlockTileSeamsTests
    {
        private const int Size = 8;

        private static readonly string[] CubeMask =
        {
            "..####..",
            ".#oooo#.",
            "#oooooo#",
            "#oooooo#",
            "#oooooo#",
            "#oooooo#",
            ".#oooo#.",
            "..####.."
        };

        private static readonly int[] VerticalRows = { 2, 3, 4, 5 };

        private static readonly Color32 Body = new(216, 144, 56, 255);

        [Test]
        public void RemoveRightOutline_ClearsTheVerticalSideButKeepsTheDiagonals()
        {
            foreach (var outline in new[] { BlockTileSeams.PropOutline, BlockTileSeams.FloorOutline })
            {
                var pixels = Cube(outline);

                Assert.IsTrue(BlockTileSeams.RemoveRightOutline(pixels, Size, Size), $"nothing removed for outline {outline}");

                foreach (var row in VerticalRows)
                {
                    Assert.AreEqual(Body, pixels[row * Size + Size - 1], $"the vertical right edge of row {row} still has an outline");
                }

                Assert.AreEqual(outline, pixels[1 * Size + 6], "the lower diagonal lost its outline and would look jagged");
                Assert.AreEqual(outline, pixels[6 * Size + 6], "the upper diagonal lost its outline and would look jagged");
                Assert.AreEqual(outline, pixels[3 * Size], "the left side must keep the outline that separates neighbours");
                Assert.IsFalse(BlockTileSeams.RemoveRightOutline(pixels, Size, Size));
                Assert.IsFalse(BlockTileSeams.HasRightOutline(pixels, Size, Size));
            }
        }

        [Test]
        public void SourceBlockTiles_ShareOneLineWithTheNeighbourOnTheRight()
        {
            var files = Directory.GetFiles(BlockTileSeams.BlockFolder, "*.png");
            Assert.IsNotEmpty(files);

            foreach (var file in files)
            {
                var texture = new Texture2D(2, 2);
                texture.LoadImage(File.ReadAllBytes(file));
                var pixels = texture.GetPixels32();

                Assert.IsFalse(BlockTileSeams.HasRightOutline(pixels, texture.width, texture.height), file);
                Assert.IsTrue(pixels.Any(BlockTileSeams.IsOutline), $"{file} lost every outline");
                Object.DestroyImmediate(texture);
            }
        }

        private static Color32[] Cube(Color32 outline)
        {
            var pixels = new Color32[Size * Size];
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    pixels[y * Size + x] = CubeMask[y][x] switch
                    {
                        '#' => outline,
                        'o' => Body,
                        _ => new Color32(0, 0, 0, 0)
                    };
                }
            }

            return pixels;
        }
    }
}
