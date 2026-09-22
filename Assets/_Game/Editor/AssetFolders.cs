using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    internal static class AssetFolders
    {
        public static void Ensure(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            Ensure(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        public static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static void Overwrite<T>(T asset, System.Action<T> configure) where T : ScriptableObject
        {
            var fresh = ScriptableObject.CreateInstance<T>();
            try
            {
                configure(fresh);
                fresh.name = asset.name;
                EditorUtility.CopySerialized(fresh, asset);
            }
            finally
            {
                Object.DestroyImmediate(fresh);
            }
        }
    }
}
