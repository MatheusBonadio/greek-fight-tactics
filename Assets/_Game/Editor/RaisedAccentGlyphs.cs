using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

namespace Game.EditorTools
{
    public static class RaisedAccentGlyphs
    {
        private const string UsedGlyphRectsProperty = "m_UsedGlyphRects";
        private const string FreeGlyphRectsProperty = "m_FreeGlyphRects";

        public static char BaseLetter(char accented) => accented.ToString().Normalize(NormalizationForm.FormD)[0];

        public static void Apply(TMP_FontAsset asset, string capitals)
        {
            var atlas = asset.atlasTexture;
            if (atlas.format != TextureFormat.Alpha8)
            {
                throw new InvalidOperationException($"{asset.name} needs an {TextureFormat.Alpha8} atlas, found {atlas.format}");
            }

            var pixels = atlas.GetPixels32();
            var occupied = asset.glyphTable.Select(glyph => Padded(glyph.glyphRect, asset.atlasPadding)).ToList();
            var meanLine = Mathf.RoundToInt(asset.faceInfo.meanLine);
            var lift = Mathf.RoundToInt(asset.faceInfo.capLine) - meanLine;

            foreach (var capital in capitals)
            {
                var accented = GlyphOf(asset, capital);
                var ink = Texels(GlyphOf(asset, BaseLetter(capital)), pixels, atlas.width).ToDictionary(texel => texel.Pen, texel => texel.Color);
                foreach (var (pen, color) in Texels(accented, pixels, atlas.width).Where(texel => texel.Pen.y >= meanLine))
                {
                    ink[pen + Vector2Int.up * lift] = color;
                }

                var bounds = BoundsOf(ink.Keys);
                var rect = Reserve(bounds.size, asset.atlasPadding, occupied, atlas.width, atlas.height);
                Erase(pixels, atlas.width, accented.glyphRect);
                Draw(accented, ink, bounds, rect, pixels, atlas.width);
            }

            atlas.SetPixels32(pixels);
            atlas.Apply(false, false);
        }

        public static void FreezeAtlas(TMP_FontAsset asset)
        {
            var serialized = new SerializedObject(asset);
            var used = serialized.FindProperty(UsedGlyphRectsProperty);
            var rects = asset.glyphTable.Select(glyph => Padded(glyph.glyphRect, asset.atlasPadding)).ToList();
            used.arraySize = rects.Count;
            for (var index = 0; index < rects.Count; index++)
            {
                var element = used.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("m_X").intValue = rects[index].x;
                element.FindPropertyRelative("m_Y").intValue = rects[index].y;
                element.FindPropertyRelative("m_Width").intValue = rects[index].width;
                element.FindPropertyRelative("m_Height").intValue = rects[index].height;
            }

            serialized.FindProperty(FreeGlyphRectsProperty).arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            asset.ReadFontAssetDefinition();
        }

        private static Glyph GlyphOf(TMP_FontAsset asset, char character) =>
            asset.characterLookupTable.TryGetValue(character, out var found)
                ? found.glyph
                : throw new InvalidOperationException($"{asset.name} has no glyph for {character}");

        private static RectInt Padded(GlyphRect rect, int padding) =>
            new(rect.x - padding, rect.y - padding, rect.width + 2 * padding, rect.height + 2 * padding);

        private static IEnumerable<(Vector2Int Pen, Color32 Color)> Texels(Glyph glyph, Color32[] pixels, int stride)
        {
            var rect = glyph.glyphRect;
            var origin = new Vector2Int(Mathf.RoundToInt(glyph.metrics.horizontalBearingX), Mathf.RoundToInt(glyph.metrics.horizontalBearingY) - rect.height);
            for (var y = 0; y < rect.height; y++)
            {
                for (var x = 0; x < rect.width; x++)
                {
                    var color = pixels[(rect.y + y) * stride + rect.x + x];
                    if (color.a > 0)
                    {
                        yield return (origin + new Vector2Int(x, y), color);
                    }
                }
            }
        }

        private static void Erase(Color32[] pixels, int stride, GlyphRect rect)
        {
            for (var y = rect.y; y < rect.y + rect.height; y++)
            {
                for (var x = rect.x; x < rect.x + rect.width; x++)
                {
                    pixels[y * stride + x].a = 0;
                }
            }
        }

        private static RectInt Reserve(Vector2Int size, int padding, List<RectInt> occupied, int width, int height)
        {
            var padded = size + Vector2Int.one * 2 * padding;
            var columns = occupied.Select(rect => rect.xMax).Prepend(0).Distinct().OrderBy(x => x).ToList();
            var rows = occupied.Select(rect => rect.yMax).Prepend(0).Distinct().OrderBy(y => y).ToList();
            foreach (var y in rows)
            {
                foreach (var x in columns)
                {
                    var slot = new RectInt(x, y, padded.x, padded.y);
                    if (slot.xMax <= width && slot.yMax <= height && !occupied.Any(slot.Overlaps))
                    {
                        occupied.Add(slot);
                        return new RectInt(slot.x + padding, slot.y + padding, size.x, size.y);
                    }
                }
            }

            throw new InvalidOperationException($"no room left in a {width}x{height} atlas for a {padded.x}x{padded.y} glyph");
        }

        private static void Draw(Glyph glyph, Dictionary<Vector2Int, Color32> ink, RectInt bounds, RectInt rect, Color32[] pixels, int stride)
        {
            foreach (var (pen, color) in ink)
            {
                pixels[(rect.y + pen.y - bounds.yMin) * stride + rect.x + pen.x - bounds.xMin] = color;
            }

            glyph.glyphRect = new GlyphRect(rect.x, rect.y, rect.width, rect.height);
            glyph.metrics = new GlyphMetrics(bounds.width, bounds.height, bounds.xMin, bounds.yMax, glyph.metrics.horizontalAdvance);
        }

        private static RectInt BoundsOf(IEnumerable<Vector2Int> ink)
        {
            var points = ink.ToList();
            var min = new Vector2Int(points.Min(point => point.x), points.Min(point => point.y));
            var max = new Vector2Int(points.Max(point => point.x), points.Max(point => point.y));
            return new RectInt(min, max - min + Vector2Int.one);
        }
    }
}
