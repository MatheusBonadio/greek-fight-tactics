using System.IO;
using System.Linq;
using Game.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Art
{
    public sealed class FloorTileSeamsTests
    {
        private const int Width = 32;
        private const int Height = 16;
        private const int InsideRowFromTop = 11;
        private const int InsideColumn = 16;

        private static readonly string[] DiamondMask =
        {
            "...............##...............",
            ".............##oo##.............",
            "...........##oooooo##...........",
            ".........##oooooooooo##.........",
            ".......##oooooooooooooo##.......",
            ".....##oooooooooooooooooo##.....",
            "...##oooooooooooooooooooooo##...",
            ".##oooooooooooooooooooooooooo##.",
            "##oooooooooooooooooooooooooooo##",
            "..##oooooooooooooooooooooooo##..",
            "....##oooooooooooooooooooo##....",
            "......###ooooooooooooooo##......",
            ".........##ooooooooooo##........",
            "...........##ooooooo##..........",
            ".............##ooo##............",
            "...............###.............."
        };

        private static readonly Color32 Outline = new(16, 24, 40, 255);
        private static readonly Color32 Body = new(248, 248, 240, 255);

        [Test]
        public void RemoveBottomOutline_KeepsOnlyTheTopEdgesSoNeighboursShareOneLine()
        {
            var pixels = Diamond();

            Assert.IsTrue(FloorTileSeams.RemoveBottomOutline(pixels, Width, Height));

            for (var fromTop = 0; fromTop < Height; fromTop++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var expected = DiamondMask[fromTop][x];
                    var actual = pixels[(Height - 1 - fromTop) * Width + x];
                    if (expected == '.')
                    {
                        Assert.AreEqual(0, actual.a, $"transparent pixel changed at {x},{fromTop}");
                    }
                    else if (fromTop <= FloorTileSeams.TipRowFromTop)
                    {
                        Assert.AreEqual(expected == '#' ? Outline : Body, actual, $"top half changed at {x},{fromTop}");
                    }
                    else
                    {
                        Assert.AreEqual(Body, actual, $"bottom edge still outlined at {x},{fromTop}");
                    }
                }
            }

            Assert.IsFalse(FloorTileSeams.RemoveBottomOutline(pixels, Width, Height));
        }

        [Test]
        public void RemoveBottomOutline_KeepsTheJointToneInsideTheTile()
        {
            var pixels = Diamond();
            var inside = (Height - 1 - InsideRowFromTop) * Width + InsideColumn;
            pixels[inside] = Outline;

            FloorTileSeams.RemoveBottomOutline(pixels, Width, Height);

            Assert.AreEqual(Outline, pixels[inside], "a texture pixel in the joint tone is not an edge");
            Assert.IsFalse(FloorTileSeams.HasBottomOutline(pixels, Width, Height));
        }

        [Test]
        public void SourceFloorTiles_HaveNoBottomOutlineButKeepTheTopOne()
        {
            var files = Directory.GetFiles(FloorTileSeams.FloorFolder, "*.png");
            Assert.IsNotEmpty(files);

            foreach (var file in files)
            {
                var texture = new Texture2D(2, 2);
                texture.LoadImage(File.ReadAllBytes(file));
                var pixels = texture.GetPixels32();

                Assert.IsFalse(FloorTileSeams.HasBottomOutline(pixels, texture.width, texture.height), file);
                var tip = pixels[(texture.height - 1 - FloorTileSeams.TipRowFromTop) * texture.width];
                Assert.AreEqual(255, tip.a, file);
                Assert.IsTrue(pixels.Take(texture.width * texture.height).Count(pixel => pixel.Equals(tip)) > 0, file);
                Object.DestroyImmediate(texture);
            }
        }

        private static Color32[] Diamond()
        {
            var pixels = new Color32[Width * Height];
            for (var fromTop = 0; fromTop < Height; fromTop++)
            {
                for (var x = 0; x < Width; x++)
                {
                    pixels[(Height - 1 - fromTop) * Width + x] = DiamondMask[fromTop][x] switch
                    {
                        '#' => Outline,
                        'o' => Body,
                        _ => new Color32(0, 0, 0, 0)
                    };
                }
            }

            return pixels;
        }
    }
}
