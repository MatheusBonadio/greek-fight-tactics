using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Content;
using Game.Core;
using Game.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    public static class DataTools
    {
        public const string CatalogPath = "Assets/_Game/Data/FallacyCatalog.asset";
        public const string TerrainCostTablePath = "Assets/_Game/Data/TerrainCostTable.asset";
        public const string FallbackLibraryPath = "Assets/_Game/Data/FallbackLibrary.asset";
        public const string UiTextPath = "Assets/_Game/Data/UiText.asset";
        public const string FallacySourcePath = "Assets/_Game/Data/Source/fallacies.json";
        public const string TerrainCostSourcePath = "Assets/_Game/Data/Source/terrain_costs.json";
        public const string UiTextSourcePath = "Assets/_Game/Data/Source/ui_text.json";
        public const string FallbackFolder = "Assets/_Game/Data/Fallback";
        public const string FallacyFolder = "Assets/_Game/Data/Fallacies";
        public const string IconFolder = "Assets/_Game/Art/Icons/Fallacies";
        public const int MinimumFallbackArguments = 3;

        public static void Regenerate() => BatchRunner.Run(RegenerateAll);

        public static void RegenerateAll()
        {
            var fallacies = RegenerateFallacies();

            var catalog = AssetFolders.LoadOrCreate<FallacyCatalogSO>(CatalogPath);
            catalog.SetEntries(fallacies);
            EditorUtility.SetDirty(catalog);

            var terrainCosts = AssetFolders.LoadOrCreate<TerrainCostTableSO>(TerrainCostTablePath);
            terrainCosts.SetCosts(JsonConvert.DeserializeObject<Dictionary<TerrainId, byte>>(File.ReadAllText(TerrainCostSourcePath)));
            EditorUtility.SetDirty(terrainCosts);

            var uiTextCount = RegenerateUiText();
            var fallbackCount = RegenerateFallbackLibrary(fallacies);

            AssetDatabase.SaveAssets();
            Debug.Log($"[DataTools] regenerated {fallacies.Count} fallacies, the terrain cost table, {uiTextCount} ui texts and {fallbackCount} fallback arguments");
        }

        private static int RegenerateUiText()
        {
            var entries = JObject.Parse(File.ReadAllText(UiTextSourcePath))["entries"]?.ToObject<Dictionary<string, string>>()
                ?? throw new InvalidOperationException($"{UiTextSourcePath} has no entries");

            var uiText = AssetFolders.LoadOrCreate<UiTextSO>(UiTextPath);
            uiText.SetEntries(entries);
            EditorUtility.SetDirty(uiText);
            return entries.Count;
        }

        private static List<FallacySO> RegenerateFallacies()
        {
            var source = JsonConvert.DeserializeObject<FallacySource>(File.ReadAllText(FallacySourcePath));
            AssetFolders.Ensure(FallacyFolder);
            AssetFolders.Ensure(IconFolder);

            var fallacies = new List<FallacySO>();
            for (var index = 0; index < source.Fallacies.Count; index++)
            {
                var entry = source.Fallacies[index];
                var icon = FallacyIconPainter.Paint($"{IconFolder}/icon_fallacy_{entry.Id}.png", entry.Category, index);
                var fallacy = AssetFolders.LoadOrCreate<FallacySO>($"{FallacyFolder}/{entry.Id}.asset");
                fallacy.Configure(entry.Id, entry.DisplayName, entry.Description, entry.PlayerHint, entry.Category, icon);
                EditorUtility.SetDirty(fallacy);
                fallacies.Add(fallacy);
            }

            return fallacies;
        }

        private static int RegenerateFallbackLibrary(IReadOnlyList<FallacySO> fallacies)
        {
            var validator = new ArgumentValidator(fallacies.Where(fallacy => fallacy.IsRefutable).Select(fallacy => fallacy.Id));
            var entries = new Dictionary<string, TextAsset>();
            var total = 0;

            foreach (var fallacy in fallacies)
            {
                var path = $"{FallbackFolder}/{fallacy.Id}.json";
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset == null)
                {
                    throw new InvalidOperationException($"missing fallback file {path}");
                }

                total += CheckFallbackFile(path, asset.text, fallacy, validator);
                entries[fallacy.Id] = asset;
            }

            var library = AssetFolders.LoadOrCreate<FallbackLibrarySO>(FallbackLibraryPath);
            library.SetEntries(entries);
            EditorUtility.SetDirty(library);
            return total;
        }

        private static int CheckFallbackFile(string path, string json, FallacySO fallacy, ArgumentValidator validator)
        {
            var declared = JArray.Parse(json).Count;
            var accepted = new List<Argument>();
            ArgumentJson.ParseValidList(json, validator, accepted);

            if (accepted.Count != declared)
            {
                throw new InvalidOperationException($"{path}: {declared - accepted.Count} of {declared} arguments fail validation");
            }

            if (accepted.Count < MinimumFallbackArguments)
            {
                throw new InvalidOperationException($"{path}: needs at least {MinimumFallbackArguments} arguments, has {accepted.Count}");
            }

            var expectedFallacyId = fallacy.IsRefutable ? fallacy.Id : null;
            var mismatch = accepted.FirstOrDefault(argument => argument.FallacyId != expectedFallacyId);
            if (mismatch.Speech != null)
            {
                throw new InvalidOperationException($"{path}: argument declares fallacy '{mismatch.FallacyId}', expected '{expectedFallacyId ?? "null"}'");
            }

            return accepted.Count;
        }

        private sealed class FallacySource
        {
            [JsonProperty("fallacies", Required = Required.Always)]
            public List<FallacyEntry> Fallacies { get; set; }
        }

        private sealed class FallacyEntry
        {
            [JsonProperty("id", Required = Required.Always)]
            public string Id { get; set; }

            [JsonProperty("category", Required = Required.Always)]
            public FallacyCategory Category { get; set; }

            [JsonProperty("display_name", Required = Required.Always)]
            public string DisplayName { get; set; }

            [JsonProperty("description", Required = Required.Always)]
            public string Description { get; set; }

            [JsonProperty("player_hint", Required = Required.Always)]
            public string PlayerHint { get; set; }
        }
    }
}
