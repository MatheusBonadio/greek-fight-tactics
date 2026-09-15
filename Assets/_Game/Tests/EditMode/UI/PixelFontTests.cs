using System.Linq;
using Game.EditorTools;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Game.Tests.EditMode.UI
{
    public sealed class PixelFontTests
    {
        [Test]
        public void DefaultFont_IsTheRasterPixelFontWithAPointFilteredStaticAtlas()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontTools.PixelFontAssetPath);
            Assert.IsNotNull(asset, "run FontTools.BuildPixelFont to create the pixel font asset");

            Assert.AreSame(asset, TMP_Settings.defaultFontAsset);
            Assert.AreEqual(GlyphRenderMode.RASTER, asset.atlasRenderMode);
            Assert.AreEqual(AtlasPopulationMode.Static, asset.atlasPopulationMode);
            Assert.AreEqual(FilterMode.Point, asset.atlasTexture.filterMode);
            Assert.IsTrue(asset.fallbackFontAssetTable.Any(fallback => fallback != null));
        }

        [Test]
        public void PixelFont_BakesEveryPortugueseCharacterItNeeds()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontTools.PixelFontAssetPath);

            Assert.IsTrue(asset.HasCharacters("áéíóúâêôãõàçÁÉÍÓÚÂÊÔÃÕÀÇ“”‘’—…", out uint[] missing, false, false),
                $"missing glyphs: {string.Join(", ", (missing ?? new uint[0]).Select(code => $"U+{code:X4}"))}");
        }
    }
}
