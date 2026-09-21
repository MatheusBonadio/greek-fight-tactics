using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.EditorTools;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace Game.Tests.EditMode.UI
{
    public sealed class PixelFontTests
    {
        private const string UppercaseAccents = "ÁÀÂÃÉÊÍÓÔÕÚÇ";
        private const byte InkThreshold = 128;

        [Test]
        public void DefaultFont_IsTheRasterPixelFontWithAPointFilteredStaticAtlas()
        {
            var asset = LoadPixelFont();

            Assert.AreSame(asset, TMP_Settings.defaultFontAsset);
            Assert.AreEqual(GlyphRenderMode.RASTER, asset.atlasRenderMode);
            Assert.AreEqual(AtlasPopulationMode.Static, asset.atlasPopulationMode);
            Assert.AreEqual(FilterMode.Point, asset.atlasTexture.filterMode);
            Assert.IsTrue(asset.fallbackFontAssetTable.Any(fallback => fallback != null));
        }

        [Test]
        public void PixelFont_BakesEveryPortugueseCharacterItNeeds()
        {
            var asset = LoadPixelFont();

            Assert.IsTrue(asset.HasCharacters("áéíóúâêôãõàçÁÀÂÃÉÊÍÓÔÕÚÇ“”‘’—…", out uint[] missing, false, false),
                $"missing glyphs: {string.Join(", ", (missing ?? new uint[0]).Select(code => $"U+{code:X4}"))}");
        }

        [Test]
        public void PixelFont_DrawsEveryUppercaseAccentOverItsFullHeightCapital()
        {
            var asset = LoadPixelFont();
            var pixels = asset.atlasTexture.GetPixels32();
            var meanLine = Mathf.RoundToInt(asset.faceInfo.meanLine);
            var capLine = Mathf.RoundToInt(asset.faceInfo.capLine);

            foreach (var capital in UppercaseAccents)
            {
                var letter = capital.ToString().Normalize(NormalizationForm.FormD)[0];
                var lowercase = char.ToLowerInvariant(capital);
                Assert.IsTrue(asset.characterLookupTable.TryGetValue(capital, out var accented), $"{capital} must come from the pixel font, not from a fallback");

                var ink = Ink(asset, accented.glyph, pixels);
                var letterInk = Ink(asset, asset.characterLookupTable[letter].glyph, pixels);
                var lowercaseInk = Ink(asset, asset.characterLookupTable[lowercase].glyph, pixels);

                Assert.AreNotEqual(asset.characterLookupTable[lowercase].glyphIndex, accented.glyphIndex, $"{capital} must not reuse the {lowercase} glyph");
                Assert.IsFalse(ink.SetEquals(lowercaseInk), $"{capital} must not be drawn like {lowercase}");
                Assert.IsTrue(ink.Except(letterInk).Any(), $"{capital} must add a diacritic to {letter}");
                CollectionAssert.AreEquivalent(
                    letterInk.Where(pen => pen.y >= meanLine && pen.y < capLine),
                    ink.Where(pen => pen.y >= meanLine && pen.y < capLine),
                    $"{capital} must keep the top of {letter} up to the cap line instead of squeezing it to the height of {lowercase}");
            }
        }

        [Test]
        public void PixelFont_RaisesTheAccentAboveTheCapitalAndKeepsItsWholeBody()
        {
            var asset = LoadPixelFont();
            var pixels = asset.atlasTexture.GetPixels32();
            var capLine = Mathf.RoundToInt(asset.faceInfo.capLine);

            foreach (var capital in FontTools.RaisedAccentCapitals)
            {
                var letter = capital.ToString().Normalize(NormalizationForm.FormD)[0];
                var ink = Ink(asset, asset.characterLookupTable[capital].glyph, pixels);
                var letterInk = Ink(asset, asset.characterLookupTable[letter].glyph, pixels);

                CollectionAssert.AreEquivalent(letterInk, ink.Where(pen => pen.y < capLine), $"{capital} must draw the whole {letter} below the cap line");
                Assert.IsTrue(ink.Any(pen => pen.y >= capLine), $"{capital} must draw its accent above the cap line");
            }
        }

        [Test]
        public void PixelFont_SpacesLinesSoRaisedAccentsNeverTouchTheDescendersAbove()
        {
            var asset = LoadPixelFont();
            var top = asset.glyphTable.Max(glyph => glyph.metrics.horizontalBearingY);
            var bottom = asset.glyphTable.Min(glyph => glyph.metrics.horizontalBearingY - glyph.metrics.height);

            Assert.GreaterOrEqual(asset.faceInfo.lineHeight, top - bottom);
        }

        private static TMP_FontAsset LoadPixelFont()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontTools.PixelFontAssetPath);
            Assert.IsNotNull(asset, "run FontTools.BuildPixelFont to create the pixel font asset");
            return asset;
        }

        private static HashSet<Vector2Int> Ink(TMP_FontAsset asset, Glyph glyph, Color32[] pixels)
        {
            var rect = glyph.glyphRect;
            var stride = asset.atlasTexture.width;
            var origin = new Vector2Int(Mathf.RoundToInt(glyph.metrics.horizontalBearingX), Mathf.RoundToInt(glyph.metrics.horizontalBearingY) - rect.height);
            var ink = new HashSet<Vector2Int>();
            for (var y = 0; y < rect.height; y++)
            {
                for (var x = 0; x < rect.width; x++)
                {
                    if (pixels[(rect.y + y) * stride + rect.x + x].a >= InkThreshold)
                    {
                        ink.Add(origin + new Vector2Int(x, y));
                    }
                }
            }

            return ink;
        }
    }
}
