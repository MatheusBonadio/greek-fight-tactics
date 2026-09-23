using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    public sealed class PlaceholderAudioImporter : AssetPostprocessor
    {
        public const string AudioFolder = "/_Game/Audio/";
        public const string MusicPrefix = "music_";

        private void OnPreprocessAudio()
        {
            if (!assetPath.Contains(AudioFolder))
            {
                return;
            }

            var importer = (UnityEditor.AudioImporter)assetImporter;
            importer.forceToMono = true;
            importer.loadInBackground = false;

            var settings = importer.defaultSampleSettings;
            settings.preloadAudioData = true;
            var streams = Path.GetFileName(assetPath).StartsWith(MusicPrefix, StringComparison.Ordinal);
            settings.loadType = streams ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = settings;
        }
    }

    public static class AudioTools
    {
        public const string SourceFolder = "audio/placeholders";
        public const string PlaceholderFolder = "Assets/_Game/Audio/Placeholder";
        public const string Extension = ".wav";

        public static void ImportPlaceholders() => BatchRunner.Run(() => CopyPlaceholders());

        public static int CopyPlaceholders()
        {
            var source = Path.GetFullPath(Path.Combine(Application.dataPath, "..", SourceFolder));
            if (!Directory.Exists(source))
            {
                throw new InvalidOperationException($"{SourceFolder} does not exist; run tools/audio/placeholders.py first");
            }

            Directory.CreateDirectory(PlaceholderFolder);
            var copied = 0;
            foreach (var file in Directory.GetFiles(source, "*" + Extension).OrderBy(path => path))
            {
                var destination = Path.Combine(PlaceholderFolder, Path.GetFileName(file));
                if (!File.Exists(destination) || !File.ReadAllBytes(file).SequenceEqual(File.ReadAllBytes(destination)))
                {
                    File.Copy(file, destination, true);
                    copied++;
                }
            }

            foreach (var stale in Directory.GetFiles(PlaceholderFolder, "*" + Extension)
                         .Where(path => !File.Exists(Path.Combine(source, Path.GetFileName(path)))))
            {
                AssetDatabase.DeleteAsset(stale.Replace('\\', '/'));
            }

            AssetDatabase.ImportAsset(PlaceholderFolder,
                ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            Debug.Log($"audio: {copied} placeholder clips copied into {PlaceholderFolder}");
            return copied;
        }

        public static string ClipPathFor(string id) => $"{PlaceholderFolder}/{id}{Extension}";
    }
}
