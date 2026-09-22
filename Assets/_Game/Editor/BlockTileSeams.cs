using System.IO;
using UnityEngine;

namespace Game.EditorTools
{
    public static class BlockTileSeams
    {
        public const string BlockFolder = ArtTools.SourceFolder + "/tiles/blocks";

        public static readonly Color32 PropOutline = new(8, 8, 16, 255);
        public static readonly Color32 FloorOutline = new(16, 24, 40, 255);

        public static bool IsOutline(Color32 pixel) => Same(pixel, PropOutline) || Same(pixel, FloorOutline);

        public static bool IsJoint(Color32 pixel, Color32? joint) => joint.HasValue ? Same(pixel, joint.Value) : IsOutline(pixel);

        public static Color32? JointOf(Color32[] pixels, int width, int height)
        {
            for (var y = height - 1; y >= 0; y--)
            {
                var pixel = pixels[y * width];
                if (pixel.a != 0)
                {
                    return pixel;
                }
            }

            return null;
        }

        public static int ApplyToSourceBlocks()
        {
            var changed = 0;
            foreach (var path in Directory.GetFiles(BlockFolder, "*.png"))
            {
                var source = new Texture2D(2, 2);
                source.LoadImage(File.ReadAllBytes(path));
                var pixels = source.GetPixels32();

                if (RemoveRightOutline(pixels, source.width, source.height))
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

        public static bool RemoveRightOutline(Color32[] pixels, int width, int height)
        {
            if (pixels.Length != width * height)
            {
                return false;
            }

            var joint = JointOf(pixels, width, height);
            var edges = RightEdges(pixels, width, height);
            var changed = false;
            for (var y = 0; y < height; y++)
            {
                var x = edges[y];
                if (x < 0 || !IsJoint(pixels[y * width + x], joint) || !IsVerticalEdge(edges, y, height))
                {
                    continue;
                }

                if (!TryBodyColor(pixels, width, y, x, joint, out var body))
                {
                    continue;
                }

                pixels[y * width + x] = body;
                changed = true;
            }

            return changed;
        }

        public static bool HasRightOutline(Color32[] pixels, int width, int height)
        {
            var joint = JointOf(pixels, width, height);
            var edges = RightEdges(pixels, width, height);
            for (var y = 0; y < height; y++)
            {
                var x = edges[y];
                if (x >= 0 && IsJoint(pixels[y * width + x], joint) && IsVerticalEdge(edges, y, height))
                {
                    return true;
                }
            }

            return false;
        }

        private static int[] RightEdges(Color32[] pixels, int width, int height)
        {
            var edges = new int[height];
            for (var y = 0; y < height; y++)
            {
                edges[y] = -1;
                for (var x = width - 1; x >= 0; x--)
                {
                    if (pixels[y * width + x].a != 0)
                    {
                        edges[y] = x;
                        break;
                    }
                }
            }

            return edges;
        }

        private static bool IsVerticalEdge(int[] edges, int y, int height) =>
            (y > 0 && edges[y - 1] == edges[y]) || (y + 1 < height && edges[y + 1] == edges[y]);

        private static bool TryBodyColor(Color32[] pixels, int width, int y, int x, Color32? joint, out Color32 body)
        {
            body = default;
            for (var step = x - 1; step >= 0; step--)
            {
                var candidate = pixels[y * width + step];
                if (candidate.a == 0)
                {
                    return false;
                }

                if (!IsJoint(candidate, joint))
                {
                    body = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool Same(Color32 left, Color32 right) =>
            left.r == right.r && left.g == right.g && left.b == right.b && left.a == right.a;
    }
}
