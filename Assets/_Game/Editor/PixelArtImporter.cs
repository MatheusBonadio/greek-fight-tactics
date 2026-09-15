using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    public sealed class PixelArtImporter : AssetPostprocessor
    {
        public const string ArtFolder = "/_Game/Art/";
        public const int PixelsPerUnit = 16;

        private void OnPreprocessTexture()
        {
            if (!assetPath.Contains(ArtFolder))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
        }
    }
}
