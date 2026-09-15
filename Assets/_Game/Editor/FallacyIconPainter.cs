using System.IO;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    internal static class FallacyIconPainter
    {
        public const int Size = 16;

        private const int FrameMin = 1;
        private const int FrameMax = Size - 2;
        private const int GlyphOrigin = 4;
        private const int GlyphStep = 3;
        private const int GlyphBlock = 2;

        private static readonly Color32 Transparent = new(0, 0, 0, 0);
        private static readonly Color32 Outline = new(8, 8, 16, 255);
        private static readonly Color32 Glyph = new(248, 248, 240, 255);
        private static readonly Color32 InformalFill = new(192, 56, 40, 255);
        private static readonly Color32 FormalFill = new(136, 56, 160, 255);
        private static readonly Color32 ParadoxFill = new(40, 144, 192, 255);

        private static readonly Vector2Int[] GlyphSlots =
        {
            new(0, 0),
            new(2, 0),
            new(1, 1),
            new(0, 2),
            new(2, 2)
        };

        public static Sprite Paint(string assetPath, FallacyCategory category, int index)
        {
            var pixels = new Color32[Size * Size];
            PaintFrame(pixels, FillFor(category));
            PaintGlyph(pixels, index + 1);

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static Color32 FillFor(FallacyCategory category) => category switch
        {
            FallacyCategory.Formal => FormalFill,
            FallacyCategory.Paradox => ParadoxFill,
            _ => InformalFill
        };

        private static void PaintFrame(Color32[] pixels, Color32 fill)
        {
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    pixels[y * Size + x] = ColorAt(x, y, fill);
                }
            }
        }

        private static Color32 ColorAt(int x, int y, Color32 fill)
        {
            var insideFrame = x >= FrameMin && x <= FrameMax && y >= FrameMin && y <= FrameMax;
            if (!insideFrame)
            {
                return Transparent;
            }

            var onVerticalEdge = x == FrameMin || x == FrameMax;
            var onHorizontalEdge = y == FrameMin || y == FrameMax;
            if (onVerticalEdge && onHorizontalEdge)
            {
                return Transparent;
            }

            return onVerticalEdge || onHorizontalEdge ? Outline : fill;
        }

        private static void PaintGlyph(Color32[] pixels, int code)
        {
            for (var slot = 0; slot < GlyphSlots.Length; slot++)
            {
                if ((code & (1 << slot)) == 0)
                {
                    continue;
                }

                var originX = GlyphOrigin + GlyphSlots[slot].x * GlyphStep;
                var originY = GlyphOrigin + GlyphSlots[slot].y * GlyphStep;
                for (var y = originY; y < originY + GlyphBlock; y++)
                {
                    for (var x = originX; x < originX + GlyphBlock; x++)
                    {
                        pixels[y * Size + x] = Glyph;
                    }
                }
            }
        }
    }
}
