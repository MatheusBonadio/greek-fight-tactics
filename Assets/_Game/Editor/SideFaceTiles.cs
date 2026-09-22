using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Grid;
using UnityEngine;

namespace Game.EditorTools
{
    public static class SideFaceTiles
    {
        public const string SideFolder = ArtTools.SourceFolder + "/tiles/sides";
        public const string Earth = "earth";
        public const string Stone = "stone";
        public const int CanvasSize = 32;
        public const int SplitColumn = 16;
        public const int BandRows = IsoMath.LevelPixels;
        public const string LipBand = "lip";
        public const string BodyBand = "body";

        public static readonly string[] Materials = { Earth, Stone };
        public static readonly string[] Bands = { LipBand, BodyBand };
        public static readonly int[] FootprintBottomRows = { 8, 8, 9, 9, 10, 10, 11, 11, 11, 12, 12, 13, 13, 14, 14, 15, 15, 15, 14, 14, 13, 13, 12, 12, 11, 11, 10, 10, 9, 9, 8, 8 };

        public static string SourceName(string material) => $"side_{material}";

        public static string TileName(string material, string band, bool southWest, bool southEast) =>
            $"side_{material}_{band}_{PartSuffix(southWest, southEast)}";

        public static IEnumerable<string> TileNames() =>
            from material in Materials
            from band in Bands
            from part in Parts
            select TileName(material, band, part.SouthWest, part.SouthEast);

        public static int GenerateSources()
        {
            var written = 0;
            foreach (var material in Materials)
            {
                var sourcePath = Path.Combine(SideFolder, SourceName(material) + ".png");
                var source = new Texture2D(2, 2);
                source.LoadImage(File.ReadAllBytes(sourcePath));
                if (source.width != CanvasSize || source.height != CanvasSize)
                {
                    throw new InvalidOperationException($"{sourcePath} must be {CanvasSize}x{CanvasSize}");
                }

                var pixels = source.GetPixels32();
                UnityEngine.Object.DestroyImmediate(source);
                foreach (var band in Bands)
                {
                    foreach (var part in Parts)
                    {
                        var segment = Segment(pixels, band == LipBand, part.SouthWest, part.SouthEast);
                        written += Write(Path.Combine(SideFolder, TileName(material, band, part.SouthWest, part.SouthEast) + ".png"), segment) ? 1 : 0;
                    }
                }
            }

            return written;
        }

        public static Color32[] Segment(Color32[] source, bool lip, bool southWest, bool southEast)
        {
            var segment = new Color32[CanvasSize * CanvasSize];
            var skip = lip ? 0 : BandRows;
            for (var x = 0; x < CanvasSize; x++)
            {
                if (x < SplitColumn ? !southWest : !southEast)
                {
                    continue;
                }

                var top = FootprintBottomRows[x] + 1;
                for (var row = 0; row < BandRows; row++)
                {
                    segment[IndexFromTop(x, top + row)] = FaceSample(source, x, top, skip + row);
                }
            }

            return segment;
        }

        private static Color32 FaceSample(Color32[] source, int x, int top, int offset)
        {
            for (var row = top + offset; row >= top; row--)
            {
                if (row < CanvasSize && source[IndexFromTop(x, row)].a != 0)
                {
                    return source[IndexFromTop(x, row)];
                }
            }

            return default;
        }

        private static int IndexFromTop(int x, int rowFromTop) => (CanvasSize - 1 - rowFromTop) * CanvasSize + x;

        private static bool Write(string path, Color32[] pixels)
        {
            var texture = new Texture2D(CanvasSize, CanvasSize, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            var bytes = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            if (File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(bytes))
            {
                return false;
            }

            File.WriteAllBytes(path, bytes);
            return true;
        }

        private static string PartSuffix(bool southWest, bool southEast) => southWest && southEast ? "sw_se" : southWest ? "sw" : "se";

        private static readonly (bool SouthWest, bool SouthEast)[] Parts = { (true, false), (false, true), (true, true) };
    }
}
