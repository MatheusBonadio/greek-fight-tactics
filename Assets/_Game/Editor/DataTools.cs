using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Battle;
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
        public const string DialogueLibraryPath = "Assets/_Game/Data/DialogueLibrary.asset";
        public const string DialogueSourcePath = "Assets/_Game/Data/Source/dialogues.json";
        public const string FallacySourcePath = "Assets/_Game/Data/Source/fallacies.json";
        public const string TerrainCostSourcePath = "Assets/_Game/Data/Source/terrain_costs.json";
        public const string UiTextSourcePath = "Assets/_Game/Data/Source/ui_text.json";
        public const string OpponentSourcePath = "Assets/_Game/Data/Source/opponents.json";
        public const string FallbackFolder = "Assets/_Game/Data/Fallback";
        public const string FallacyFolder = "Assets/_Game/Data/Fallacies";
        public const string OpponentFolder = "Assets/_Game/Data/Opponents";
        public const string IconFolder = "Assets/_Game/Art/Icons/Fallacies";
        public const int MinimumFallbackArguments = 3;

        public static void Regenerate() => BatchRunner.Run(RegenerateAll);

        public static string OpponentPath(string opponentId) => $"{OpponentFolder}/{opponentId}.asset";

        public static IReadOnlyList<string> OpponentIds() => LoadOpponentSource().Opponents.Select(entry => entry.Id).ToList();

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
            var opponentCount = RegenerateOpponents(fallacies);
            var fallbackCount = RegenerateFallbackLibrary(fallacies);
            var dialogueCount = RegenerateDialogues();

            AssetDatabase.SaveAssets();
            Debug.Log($"[DataTools] regenerated {fallacies.Count} fallacies, the terrain cost table, {uiTextCount} ui texts, {opponentCount} opponents, {fallbackCount} fallback arguments and {dialogueCount} dialogues");
        }

        public static (IReadOnlyDictionary<string, DialogueSpeaker> Speakers, IReadOnlyList<DialogueScript> Scripts) LoadDialogueSource(string path)
        {
            var source = JsonConvert.DeserializeObject<DialogueSource>(File.ReadAllText(path))
                ?? throw new InvalidOperationException($"{path} is not a dialogue file");

            var speakers = source.Speakers.ToDictionary(pair => pair.Key, pair => new DialogueSpeaker(pair.Value.Name, pair.Value.Portrait ?? string.Empty));
            var scripts = source.Dialogues.Select(entry => entry.ToScript()).ToList();

            var errors = new List<string>();
            errors.AddRange(scripts.GroupBy(script => script.Id).Where(group => group.Count() > 1).Select(group => $"dialogue {group.Key} is declared {group.Count()} times"));
            errors.AddRange(speakers.Where(pair => string.IsNullOrWhiteSpace(pair.Value.DisplayName)).Select(pair => $"speaker {pair.Key} has no name"));
            foreach (var script in scripts)
            {
                errors.AddRange(script.Validate());
                errors.AddRange(script.AllLines()
                    .Where(line => !line.IsNarration && !speakers.ContainsKey(line.SpeakerId))
                    .Select(line => $"{script.Id}: unknown speaker {line.SpeakerId}"));
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException($"{path}: {string.Join("; ", errors)}");
            }

            return (speakers, scripts);
        }

        private static int RegenerateDialogues()
        {
            var (speakers, scripts) = LoadDialogueSource(DialogueSourcePath);
            var library = AssetFolders.LoadOrCreate<DialogueLibrarySO>(DialogueLibraryPath);
            AssetFolders.Overwrite(library, fresh => fresh.SetEntries(speakers, scripts));
            EditorUtility.SetDirty(library);
            return scripts.Count;
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

        private static int RegenerateOpponents(IReadOnlyList<FallacySO> fallacies)
        {
            var source = LoadOpponentSource();
            var byId = fallacies.ToDictionary(fallacy => fallacy.Id);
            var dialogueIds = new HashSet<string>(LoadDialogueSource(DialogueSourcePath).Scripts.Select(script => script.Id));
            AssetFolders.Ensure(OpponentFolder);

            foreach (var entry in source.Opponents)
            {
                if (entry.AggroRadius < 0)
                {
                    throw new InvalidOperationException($"{OpponentSourcePath}: opponent {entry.Id} has a negative aggro_radius");
                }

                var pool = entry.FallacyPool
                    .Select(fallacyId => byId.TryGetValue(fallacyId, out var fallacy)
                        ? fallacy
                        : throw new InvalidOperationException($"{OpponentSourcePath}: opponent {entry.Id} references unknown fallacy {fallacyId}"))
                    .ToList();

                var opponent = AssetFolders.LoadOrCreate<OpponentSO>(OpponentPath(entry.Id));
                opponent.Configure(entry.Id, entry.DisplayName, entry.MaxCoherence, entry.InitialAudience, pool, entry.Act, entry.Profile);
                opponent.ConfigurePresence(AssetDatabase.LoadAssetAtPath<Texture2D>(ArtTools.SheetPathFor(entry.Id)), entry.AggroRadius);
                opponent.ConfigureTutorial(ToTutorial(entry, pool, dialogueIds));
                EditorUtility.SetDirty(opponent);
            }

            return source.Opponents.Count;
        }

        private static OpponentTutorial ToTutorial(OpponentEntry entry, IReadOnlyList<FallacySO> pool, ISet<string> dialogueIds)
        {
            var source = entry.Tutorial;
            if (source == null)
            {
                return new OpponentTutorial();
            }

            var where = $"{OpponentSourcePath}: the tutorial of {entry.Id}";
            var fallacy = pool.FirstOrDefault(candidate => candidate.Id == source.Fallacy)
                ?? throw new InvalidOperationException($"{where} teaches {source.Fallacy}, which is not in its fallacy_pool");
            if (!fallacy.IsRefutable)
            {
                throw new InvalidOperationException($"{where} teaches {fallacy.Id}, a paradox that REFUTE cannot answer");
            }

            if (!SaveFlags.IsCustomName(source.DoneFlag))
            {
                throw new InvalidOperationException($"{where} has the invalid done_flag {source.DoneFlag}");
            }

            var dialogues = new Dictionary<TutorialCue, string>();
            foreach (var pair in source.Dialogues ?? new Dictionary<string, string>())
            {
                var cue = Enum.GetValues(typeof(TutorialCue)).Cast<TutorialCue>()
                    .FirstOrDefault(value => string.Equals(value.ToString(), pair.Key, StringComparison.OrdinalIgnoreCase));
                if (cue == TutorialCue.None)
                {
                    throw new InvalidOperationException($"{where} has the unknown cue {pair.Key}");
                }

                if (!dialogueIds.Contains(pair.Value ?? string.Empty))
                {
                    throw new InvalidOperationException($"{where} plays the unknown dialogue {pair.Value} on {pair.Key}");
                }

                dialogues[cue] = pair.Value;
            }

            return new OpponentTutorial(fallacy.Id, source.DoneFlag, dialogues);
        }

        private static OpponentSource LoadOpponentSource()
        {
            var source = JsonConvert.DeserializeObject<OpponentSource>(File.ReadAllText(OpponentSourcePath))
                ?? throw new InvalidOperationException($"{OpponentSourcePath} is empty");
            var repeated = source.Opponents.GroupBy(entry => entry.Id).FirstOrDefault(group => group.Count() > 1);
            if (repeated != null)
            {
                throw new InvalidOperationException($"{OpponentSourcePath}: opponent {repeated.Key} is declared twice");
            }

            return source;
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

        private sealed class DialogueSource
        {
            [JsonProperty("speakers", Required = Required.Always)]
            public Dictionary<string, SpeakerSource> Speakers { get; set; }

            [JsonProperty("dialogues", Required = Required.Always)]
            public List<DialogueSourceEntry> Dialogues { get; set; }
        }

        private sealed class SpeakerSource
        {
            [JsonProperty("name", Required = Required.Always)]
            public string Name { get; set; }

            [JsonProperty("portrait")]
            public string Portrait { get; set; }
        }

        private sealed class DialogueSourceEntry
        {
            [JsonProperty("id", Required = Required.Always)]
            public string Id { get; set; }

            [JsonProperty("lines", Required = Required.Always)]
            public List<DialogueLineSource> Lines { get; set; }

            public DialogueScript ToScript() => new(Id, Lines.Select(line => line.ToLine()));
        }

        private sealed class DialogueLineSource
        {
            [JsonProperty("speaker")]
            public string Speaker { get; set; }

            [JsonProperty("text", Required = Required.Always)]
            public string Text { get; set; }

            [JsonProperty("conditions")]
            public List<ConditionSource> Conditions { get; set; } = new();

            [JsonProperty("effects")]
            public List<EffectSource> Effects { get; set; } = new();

            [JsonProperty("choices")]
            public List<ChoiceSource> Choices { get; set; } = new();

            public ScriptLine ToLine() => new(
                Speaker,
                Text,
                (Conditions ?? new List<ConditionSource>()).Select(condition => new StoryCondition(condition.Flag, condition.EqualsValue, condition.Absent)),
                (Effects ?? new List<EffectSource>()).Select(effect => effect.ToEffect()),
                (Choices ?? new List<ChoiceSource>()).Select(choice => choice.ToChoice()));
        }

        private sealed class ChoiceSource
        {
            [JsonProperty("text", Required = Required.Always)]
            public string Text { get; set; }

            [JsonProperty("effects")]
            public List<EffectSource> Effects { get; set; } = new();

            [JsonProperty("lines")]
            public List<DialogueLineSource> Lines { get; set; } = new();

            public DialogueChoice ToChoice() => new(
                Text,
                (Effects ?? new List<EffectSource>()).Select(effect => effect.ToEffect()),
                (Lines ?? new List<DialogueLineSource>()).Select(line => line.ToLine()));
        }

        private sealed class ConditionSource
        {
            [JsonProperty("flag", Required = Required.Always)]
            public string Flag { get; set; }

            [JsonProperty("equals")]
            public string EqualsValue { get; set; }

            [JsonProperty("absent")]
            public bool Absent { get; set; }
        }

        private sealed class EffectSource
        {
            [JsonProperty("flag", Required = Required.Always)]
            public string Flag { get; set; }

            [JsonProperty("value")]
            public string Value { get; set; }

            public StoryEffect ToEffect() => new(Flag, Value);
        }

        private sealed class OpponentSource
        {
            [JsonProperty("opponents", Required = Required.Always)]
            public List<OpponentEntry> Opponents { get; set; }
        }

        private sealed class OpponentEntry
        {
            [JsonProperty("id", Required = Required.Always)]
            public string Id { get; set; }

            [JsonProperty("display_name", Required = Required.Always)]
            public string DisplayName { get; set; }

            [JsonProperty("profile", Required = Required.Always)]
            public Profile Profile { get; set; }

            [JsonProperty("act", Required = Required.Always)]
            public Act Act { get; set; }

            [JsonProperty("max_coherence", Required = Required.Always)]
            public int MaxCoherence { get; set; }

            [JsonProperty("initial_audience", Required = Required.Always)]
            public int InitialAudience { get; set; }

            [JsonProperty("fallacy_pool", Required = Required.Always)]
            public List<string> FallacyPool { get; set; }

            [JsonProperty("aggro_radius")]
            public int AggroRadius { get; set; } = AggroRule.DefaultRadius;

            [JsonProperty("tutorial")]
            public TutorialSource Tutorial { get; set; }
        }

        private sealed class TutorialSource
        {
            [JsonProperty("fallacy", Required = Required.Always)]
            public string Fallacy { get; set; }

            [JsonProperty("done_flag", Required = Required.Always)]
            public string DoneFlag { get; set; }

            [JsonProperty("dialogues")]
            public Dictionary<string, string> Dialogues { get; set; } = new();
        }
    }
}
