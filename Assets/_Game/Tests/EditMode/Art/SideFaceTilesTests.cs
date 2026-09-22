using System.IO;
using System.Linq;
using Game.EditorTools;
using Game.Grid;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Art
{
    public sealed class SideFaceTilesTests
    {
        private const string FloorFolder = ArtTools.SourceFolder + "/tiles/floor";

        [Test]
        public void FootprintBottom_IsTheLowestRowOfEveryFloorTile()
        {
            var floors = Directory.GetFiles(FloorFolder, "*.png");
            Assert.IsNotEmpty(floors);
            foreach (var path in floors)
            {
                var pixels = Load(path, out var width, out var height);
                for (var x = 0; x < width; x++)
                {
                    var lowest = Enumerable.Range(0, height).Where(row => pixels[(height - 1 - row) * width + x].a != 0).DefaultIfEmpty(-1).Max();
                    Assert.AreEqual(SideFaceTiles.FootprintBottomRows[x], lowest, $"{path} column {x}");
                }
            }
        }

        [Test]
        public void EverySegment_IsGeneratedFromItsSourceAndCommitted()
        {
            var names = SideFaceTiles.TileNames().ToList();
            Assert.AreEqual(SideFaceTiles.Materials.Length * SideFaceTiles.Bands.Length * 3, names.Count);
            foreach (var material in SideFaceTiles.Materials)
            {
                var source = Load(Path.Combine(SideFaceTiles.SideFolder, SideFaceTiles.SourceName(material) + ".png"), out _, out _);
                foreach (var band in SideFaceTiles.Bands)
                {
                    foreach (var (southWest, southEast) in new[] { (true, false), (false, true), (true, true) })
                    {
                        var name = SideFaceTiles.TileName(material, band, southWest, southEast);
                        var path = Path.Combine(SideFaceTiles.SideFolder, name + ".png");
                        Assert.IsTrue(File.Exists(path), $"run SandboxTools.BuildSandbox to generate {path}");
                        var expected = SideFaceTiles.Segment(source, band == SideFaceTiles.LipBand, southWest, southEast);
                        var committed = Load(path, out _, out _);
                        Assert.IsTrue(expected.Select(Key).SequenceEqual(committed.Select(Key)), $"{name} is out of date with its source");
                    }
                }
            }
        }

        [Test]
        public void Segment_HangsOneLevelBelowTheFloorEdgeOnlyOnItsSides()
        {
            var source = Load(Path.Combine(SideFaceTiles.SideFolder, SideFaceTiles.SourceName(SideFaceTiles.Earth) + ".png"), out _, out _);
            var size = SideFaceTiles.CanvasSize;
            var southWest = SideFaceTiles.Segment(source, true, true, false);

            for (var x = 0; x < size; x++)
            {
                var rows = Enumerable.Range(0, size).Where(row => southWest[(size - 1 - row) * size + x].a != 0).ToList();
                if (x >= SideFaceTiles.SplitColumn)
                {
                    CollectionAssert.IsEmpty(rows, $"column {x} belongs to the south east face");
                    continue;
                }

                var top = SideFaceTiles.FootprintBottomRows[x] + 1;
                CollectionAssert.AreEqual(Enumerable.Range(top, IsoMath.LevelPixels), rows, $"column {x} fills one level right under the edge");
            }
        }

        private static string Key(Color32 pixel) => pixel.a == 0 ? "clear" : $"{pixel.r},{pixel.g},{pixel.b},{pixel.a}";

        private static Color32[] Load(string path, out int width, out int height)
        {
            var texture = new Texture2D(2, 2);
            texture.LoadImage(File.ReadAllBytes(path));
            width = texture.width;
            height = texture.height;
            var pixels = texture.GetPixels32();
            Object.DestroyImmediate(texture);
            return pixels;
        }
    }
}
