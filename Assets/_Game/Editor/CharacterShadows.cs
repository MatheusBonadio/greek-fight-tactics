using System.IO;
using System.Linq;
using UnityEngine;

namespace Game.EditorTools
{
    public static class CharacterShadows
    {
        public const string SourceCharacterFolder = ArtTools.SourceFolder + "/characters";
        public const byte Opaque = 255;

        public static int RemoveFromSourceCharacters()
        {
            var cleaned = 0;
            foreach (var path in Directory.GetFiles(SourceCharacterFolder, "*.png", SearchOption.AllDirectories))
            {
                if (RemoveFrom(path))
                {
                    cleaned++;
                }
            }

            return cleaned;
        }

        private static bool RemoveFrom(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path)))
            {
                Object.DestroyImmediate(texture);
                return false;
            }

            var pixels = texture.GetPixels32();
            var painted = false;
            for (var index = 0; index < pixels.Length; index++)
            {
                if (pixels[index].a == 0 || pixels[index].a == Opaque)
                {
                    continue;
                }

                pixels[index] = new Color32(0, 0, 0, 0);
                painted = true;
            }

            if (painted)
            {
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }

            Object.DestroyImmediate(texture);
            return painted;
        }
    }
}
