using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;

namespace Game.EditorTools
{
    public static class BuildProfileTools
    {
        public const string ProfileFolder = "Assets/Settings/Build Profiles";
        public const string WindowsDevelopmentName = "Windows Development";
        public const string WindowsReleaseName = "Windows Release";
        public const string WebName = "Web";

        private const string WindowsPlatformDisplayName = "Windows";
        private const string WebPlatformDisplayName = "Web";
        private const string DevelopmentPropertyName = "m_Development";

        private static readonly ProfileSpec[] Specs =
        {
            new(WindowsDevelopmentName, WindowsPlatformDisplayName, true),
            new(WindowsReleaseName, WindowsPlatformDisplayName, false),
            new(WebName, WebPlatformDisplayName, false)
        };

        public static void CreateProfiles() => BatchRunner.Run(CreateMissingProfiles);

        public static void ConfigureProfiles() => BatchRunner.Run(ConfigureExistingProfiles);

        public static string PathFor(string profileName) => $"{ProfileFolder}/{profileName}.asset";

        public static bool TryReadDevelopmentFlag(BuildProfile profile, out bool development)
        {
            development = false;
            if (!TryFindDevelopmentProperty(new SerializedObject(profile), out var property))
            {
                return false;
            }

            development = property.boolValue;
            return true;
        }

        public static void CreateMissingProfiles()
        {
            foreach (var spec in Specs)
            {
                if (File.Exists(PathFor(spec.Name)))
                {
                    continue;
                }

                BuildProfile.CreateBuildProfile(FindPlatformGuid(spec.PlatformDisplayName), spec.Name, null);
                Debug.Log($"[BuildProfileTools] created {PathFor(spec.Name)}");
            }

            AssetDatabase.SaveAssets();
        }

        public static void ConfigureExistingProfiles()
        {
            foreach (var spec in Specs)
            {
                var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(PathFor(spec.Name));
                if (profile == null)
                {
                    throw new InvalidOperationException($"missing build profile {PathFor(spec.Name)}; run CreateProfiles first");
                }

                var serialized = new SerializedObject(profile);
                if (!TryFindDevelopmentProperty(serialized, out var property))
                {
                    throw new InvalidOperationException($"{PathFor(spec.Name)} has no {DevelopmentPropertyName} setting");
                }

                property.boolValue = spec.Development;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(profile);
                Debug.Log($"[BuildProfileTools] {spec.Name}: development = {spec.Development}");
            }

            AssetDatabase.SaveAssets();
        }

        private static GUID FindPlatformGuid(string displayName)
        {
            foreach (var platform in BuildProfile.GetInstalledPlatformModules())
            {
                if (platform.displayName == displayName)
                {
                    return platform.platformGuid;
                }
            }

            throw new InvalidOperationException($"platform module '{displayName}' is not installed");
        }

        private static bool TryFindDevelopmentProperty(SerializedObject serialized, out SerializedProperty property)
        {
            var iterator = serialized.GetIterator();
            while (iterator.Next(true))
            {
                if (iterator.name == DevelopmentPropertyName && iterator.propertyType == SerializedPropertyType.Boolean)
                {
                    property = iterator.Copy();
                    return true;
                }
            }

            property = null;
            return false;
        }

        private readonly struct ProfileSpec
        {
            public readonly string Name;
            public readonly string PlatformDisplayName;
            public readonly bool Development;

            public ProfileSpec(string name, string platformDisplayName, bool development)
            {
                Name = name;
                PlatformDisplayName = platformDisplayName;
                Development = development;
            }
        }
    }
}
