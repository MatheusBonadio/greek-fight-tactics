using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    public sealed class PixelArtImporter : AssetPostprocessor
    {
        public const string ArtFolder = "/_Game/Art/";
        public const string TileFolder = "/tiles/";
        public const string SheetSuffix = "_sheet.png";
        public const string CharacterFolder = "/characters/";
        public const string UiFolder = "/ui/";
        public const string UiBoxPrefix = "box_";
        public const int PixelsPerUnit = 16;
        public const int UiBoxBorder = 2;

        private void OnPreprocessTexture()
        {
            if (!assetPath.Contains(ArtFolder))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = IsSliced(assetPath) ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            if (assetPath.Contains(TileFolder))
            {
                settings.spriteAlignment = (int)SpriteAlignment.BottomLeft;
            }

            if (IsUiBox(assetPath))
            {
                settings.spriteBorder = new Vector4(UiBoxBorder, UiBoxBorder, UiBoxBorder, UiBoxBorder);
            }

            importer.SetTextureSettings(settings);
        }

        public static bool IsUiBox(string path) =>
            path.Contains(UiFolder) && System.IO.Path.GetFileName(path).StartsWith(UiBoxPrefix);

        public static bool IsStrip(string path) =>
            path.Contains(CharacterFolder) && !path.EndsWith(SheetSuffix)
            && (path.EndsWith("_se.png") || path.EndsWith("_ne.png"));

        public static bool IsSliced(string path) => path.EndsWith(SheetSuffix) || IsStrip(path);
    }
}
