using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    public sealed class ProjectAudioImporter : AssetPostprocessor
    {
        public const string AudioFolder = "/_Game/Audio/";
        public const string MusicFolder = "/_Game/Audio/Music/";
        public const float MusicQuality = 0.7f;

        private void OnPreprocessAudio()
        {
            if (!assetPath.Contains(AudioFolder))
            {
                return;
            }

            var importer = (UnityEditor.AudioImporter)assetImporter;
            importer.loadInBackground = false;
            importer.defaultSampleSettings = assetPath.Contains(MusicFolder) ? RecordedMusic(importer) : Placeholder(importer);
        }

        private static AudioImporterSampleSettings RecordedMusic(UnityEditor.AudioImporter importer)
        {
            importer.forceToMono = false;
            var settings = importer.defaultSampleSettings;
            settings.preloadAudioData = false;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = MusicQuality;
            return settings;
        }

        private static AudioImporterSampleSettings Placeholder(UnityEditor.AudioImporter importer)
        {
            importer.forceToMono = true;
            var settings = importer.defaultSampleSettings;
            settings.preloadAudioData = true;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            return settings;
        }
    }

    public static class AudioTools
    {
        public const string PlaceholderSource = "audio/placeholders";
        public const string MusicSource = "audio/music";
        public const string PlaceholderFolder = "Assets/_Game/Audio/Placeholder";
        public const string MusicFolder = "Assets/_Game/Audio/Music";
        public const string Extension = ".wav";

        public static void ImportAudio() => BatchRunner.Run(() => CopyAudio());

        public static int CopyAudio() => Mirror(PlaceholderSource, PlaceholderFolder) + Mirror(MusicSource, MusicFolder);

        public static string ClipPathFor(string id)
        {
            var recorded = $"{MusicFolder}/{id}{Extension}";
            return File.Exists(recorded) ? recorded : $"{PlaceholderFolder}/{id}{Extension}";
        }

        private static int Mirror(string sourceFolder, string assetFolder)
        {
            var source = Path.GetFullPath(Path.Combine(Application.dataPath, "..", sourceFolder));
            if (!Directory.Exists(source))
            {
                throw new InvalidOperationException($"{sourceFolder} does not exist");
            }

            Directory.CreateDirectory(assetFolder);
            var copied = 0;
            foreach (var file in Directory.GetFiles(source, "*" + Extension).OrderBy(path => path))
            {
                var destination = Path.Combine(assetFolder, Path.GetFileName(file));
                if (!File.Exists(destination) || !File.ReadAllBytes(file).SequenceEqual(File.ReadAllBytes(destination)))
                {
                    File.Copy(file, destination, true);
                    copied++;
                }
            }

            foreach (var stale in Directory.GetFiles(assetFolder, "*" + Extension)
                         .Where(path => !File.Exists(Path.Combine(source, Path.GetFileName(path)))))
            {
                AssetDatabase.DeleteAsset(stale.Replace('\\', '/'));
            }

            AssetDatabase.ImportAsset(assetFolder,
                ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            Debug.Log($"audio: {copied} clips copied from {sourceFolder} into {assetFolder}");
            return copied;
        }
    }
}
