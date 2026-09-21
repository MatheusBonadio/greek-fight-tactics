using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace Game.EditorTools
{
    public static class FontTools
    {
        public const string PixelFontPath = "Assets/_Game/Fonts/PressStart2P/PressStart2P-Regular.ttf";
        public const string PixelFontAssetPath = "Assets/_Game/Fonts/PressStart2P/PressStart2P Pixel.asset";
        public const string FallbackFontAssetPath = UiTools.TextMeshProFolder + "/Resources/Fonts & Materials/LiberationSans SDF.asset";
        public const int SamplingPointSize = 16;
        public const int AtlasPadding = 1;
        public const int AtlasSize = 512;
        public const string Characters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "áéíóúâêôãõàçÁÉÍÓÚÂÊÔÃÕÀÇ“”‘’—…·ºª°";
        public const string RaisedAccentCapitals = "ÁÀÂÃÉÊÍÓÔÕÚ";

        private const string DefaultFontProperty = "m_defaultFontAsset";

        public static void BuildPixelFont() => BatchRunner.Run(() => BakePixelFont());

        public static TMP_FontAsset BakePixelFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(PixelFontPath)
                ?? throw new InvalidOperationException($"missing {PixelFontPath}");

            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PixelFontAssetPath) ?? CreatePixelFont(font);
            Bake(asset, font);
            SetAsDefault(asset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[FontTools] default font is {PixelFontAssetPath}");
            return asset;
        }

        private static TMP_FontAsset CreatePixelFont(Font font)
        {
            var asset = TMP_FontAsset.CreateFontAsset(font, SamplingPointSize, AtlasPadding, GlyphRenderMode.RASTER, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, false);
            asset.name = System.IO.Path.GetFileNameWithoutExtension(PixelFontAssetPath);
            AssetDatabase.CreateAsset(asset, PixelFontAssetPath);

            var atlas = asset.atlasTexture;
            atlas.name = $"{asset.name} Atlas";
            AssetDatabase.AddObjectToAsset(atlas, asset);
            asset.material.name = $"{asset.name} Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            return asset;
        }

        private static void Bake(TMP_FontAsset asset, Font font)
        {
            if (FontEngine.LoadFontFace(font, SamplingPointSize) != FontEngineError.Success)
            {
                throw new InvalidOperationException($"could not load {PixelFontPath}");
            }

            asset.ClearFontAssetData();
            asset.faceInfo = FontEngine.GetFaceInfo();
            asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            if (!asset.TryAddCharacters(Characters, out var missing))
            {
                Debug.LogWarning($"[FontTools] {PixelFontPath} has no glyph for: {missing}");
            }

            RaisedAccentGlyphs.Apply(asset, RaisedAccentCapitals);
            asset.faceInfo = WithLeadingForEveryGlyph(asset);
            asset.atlasTexture.filterMode = FilterMode.Point;
            asset.atlasPopulationMode = AtlasPopulationMode.Static;

            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackFontAssetPath);
            if (fallback != null)
            {
                asset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
            }

            RaisedAccentGlyphs.FreezeAtlas(asset);
            EditorUtility.SetDirty(asset.atlasTexture);
            EditorUtility.SetDirty(asset);
        }

        private static FaceInfo WithLeadingForEveryGlyph(TMP_FontAsset asset)
        {
            var face = asset.faceInfo;
            var top = asset.glyphTable.Max(glyph => glyph.metrics.horizontalBearingY);
            var bottom = asset.glyphTable.Min(glyph => glyph.metrics.horizontalBearingY - glyph.metrics.height);
            face.lineHeight = Mathf.Max(face.ascentLine - face.descentLine, top - bottom);
            return face;
        }

        private static void SetAsDefault(TMP_FontAsset asset)
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(UiTools.TextMeshProSettingsPath)
                ?? throw new InvalidOperationException($"missing {UiTools.TextMeshProSettingsPath}; run UiTools.ImportTextMeshProEssentials first");

            var serialized = new SerializedObject(settings);
            serialized.FindProperty(DefaultFontProperty).objectReferenceValue = asset;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }
    }
}
