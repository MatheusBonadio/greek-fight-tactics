using System;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEditor;
using Debug = UnityEngine.Debug;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Game.EditorTools
{
    public static class UiTools
    {
        public const string TextMeshProFolder = "Assets/TextMesh Pro";
        public const string TextMeshProSettingsPath = TextMeshProFolder + "/Resources/TMP Settings.asset";
        public const string EssentialResourcesPackage = "Package Resources/TMP Essential Resources.unitypackage";

        private const string AssetsPrefix = "Assets/";
        private const string PathnameFile = "pathname";
        private const string AssetFile = "asset";
        private const string MetaFile = "asset.meta";

        public static void ImportTextMeshProEssentials() => BatchRunner.Run(EnsureTextMeshProEssentials);

        public static void EnsureTextMeshProEssentials()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>(TextMeshProSettingsPath) != null)
            {
                Debug.Log("[UiTools] TextMesh Pro essentials already imported");
                return;
            }

            var package = PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly)
                ?? throw new InvalidOperationException("TextMesh Pro package not found");
            var unityPackage = Path.Combine(package.resolvedPath, EssentialResourcesPackage);
            var copied = ExtractUnityPackage(unityPackage);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>(TextMeshProSettingsPath) == null)
            {
                throw new InvalidOperationException($"extracted {copied} files but {TextMeshProSettingsPath} did not import");
            }

            Debug.Log($"[UiTools] imported TextMesh Pro essentials ({copied} files)");
        }

        public static int ExtractUnityPackage(string unityPackage)
        {
            if (!File.Exists(unityPackage))
            {
                throw new FileNotFoundException("unity package missing", unityPackage);
            }

            var temporary = Path.Combine(Path.GetTempPath(), $"gft-unitypackage-{Guid.NewGuid():N}");
            Directory.CreateDirectory(temporary);
            try
            {
                RunTar(unityPackage, temporary);
                var copied = 0;
                foreach (var entry in Directory.GetDirectories(temporary))
                {
                    copied += CopyEntry(entry);
                }

                return copied;
            }
            finally
            {
                Directory.Delete(temporary, true);
            }
        }

        private static int CopyEntry(string entry)
        {
            var pathnamePath = Path.Combine(entry, PathnameFile);
            if (!File.Exists(pathnamePath))
            {
                return 0;
            }

            var target = File.ReadAllLines(pathnamePath)[0].Trim().Replace('\\', '/');
            if (!target.StartsWith(AssetsPrefix, StringComparison.Ordinal) || target.Contains(".."))
            {
                throw new InvalidOperationException($"unity package entry escapes Assets: {target}");
            }

            var assetPath = Path.Combine(entry, AssetFile);
            if (File.Exists(assetPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? AssetsPrefix);
                File.Copy(assetPath, target, true);
            }
            else
            {
                Directory.CreateDirectory(target);
            }

            var metaPath = Path.Combine(entry, MetaFile);
            if (File.Exists(metaPath))
            {
                File.Copy(metaPath, target + ".meta", true);
            }

            return File.Exists(assetPath) ? 1 : 0;
        }

        private static void RunTar(string archive, string destination)
        {
            var systemTar = Path.Combine(Environment.SystemDirectory, "tar.exe");
            var startInfo = new ProcessStartInfo(File.Exists(systemTar) ? systemTar : "tar", $"-xzf \"{archive}\" -C \"{destination}\"")
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("could not start tar");
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"tar exited with {process.ExitCode}: {error}");
            }
        }
    }
}
