using System.IO;
using UnityEngine;

namespace Game.EditorTools
{
    public static class FloorTileSeams
    {
        public const string FloorFolder = ArtTools.SourceFolder + "/tiles/floor";
        public const int TipRowFromTop = 8;

        public static int ApplyToSourceFloors()
        {
            var changed = 0;
            foreach (var path in Directory.GetFiles(FloorFolder, "*.png"))
            {
                var source = new Texture2D(2, 2);
                source.LoadImage(File.ReadAllBytes(path));
                var pixels = source.GetPixels32();

                if (RemoveBottomOutline(pixels, source.width, source.height))
                {
                    var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                    texture.SetPixels32(pixels);
                    texture.Apply();
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                    Object.DestroyImmediate(texture);
                    changed++;
                }

                Object.DestroyImmediate(source);
            }

            return changed;
        }

        public static bool RemoveBottomOutline(Color32[] pixels, int width, int height)
        {
            var tipRow = height - 1 - TipRowFromTop;
            if (tipRow <= 0 || pixels.Length != width * height)
            {
                return false;
            }

            var outline = pixels[tipRow * width];
            if (outline.a == 0)
            {
                return false;
            }

            var changed = false;
            for (var y = 0; y < tipRow; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = y * width + x;
                    if (!Same(pixels[index], outline))
                    {
                        continue;
                    }

                    var above = y + 1;
                    while (above < height && Same(pixels[above * width + x], outline))
                    {
                        above++;
                    }

                    if (above >= height || pixels[above * width + x].a == 0)
                    {
                        continue;
                    }

                    pixels[index] = pixels[above * width + x];
                    changed = true;
                }
            }

            return changed;
        }

        public static bool HasBottomOutline(Color32[] pixels, int width, int height)
        {
            var tipRow = height - 1 - TipRowFromTop;
            var outline = pixels[tipRow * width];
            for (var y = 0; y < tipRow; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (Same(pixels[y * width + x], outline))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool Same(Color32 left, Color32 right) =>
            left.r == right.r && left.g == right.g && left.b == right.b && left.a == right.a;
    }
}
