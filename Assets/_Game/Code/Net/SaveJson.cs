using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Newtonsoft.Json;
using Unity.Scripting.LifecycleManagement;

namespace Game.Net
{
    public static class SaveJson
    {
        [NoAutoStaticsCleanup]
        private static readonly JsonSerializerSettings Settings = new()
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        public static string Serialize(SaveData save, bool indented = true)
        {
            var document = new SaveDocument
            {
                ActFlag = (int)save.ActFlag,
                FogBitmask = Convert.ToBase64String(save.FogBitmask ?? Array.Empty<byte>()),
                DamarisBranch = save.DamarisBranch.ToString().ToLowerInvariant(),
                Reputation = save.Reputation,
                Inventory = new Dictionary<string, int>(save.Inventory),
                Position = new PositionDocument { X = save.Position.X, Y = save.Position.Y },
                Location = save.Location ?? string.Empty,
                PlaytimeSeconds = save.PlaytimeSeconds,
                Timestamp = save.Timestamp,
                Duels = save.Duels.ToDictionary(pair => pair.Key, pair => new DuelDocument { Won = pair.Value.Won, Victory = pair.Value.Victory.ToString().ToLowerInvariant() }),
                Flags = new Dictionary<string, string>(save.Flags)
            };

            return JsonConvert.SerializeObject(document, indented ? Formatting.Indented : Formatting.None, Settings);
        }

        public static bool TryDeserialize(string json, out SaveData save, out string error)
        {
            save = null;
            SaveDocument document;
            try
            {
                document = JsonConvert.DeserializeObject<SaveDocument>(json ?? string.Empty, Settings);
            }
            catch (JsonException exception)
            {
                error = exception.Message;
                return false;
            }

            if (document == null)
            {
                error = "empty save";
                return false;
            }

            if (!Enum.IsDefined(typeof(Act), document.ActFlag))
            {
                error = $"invalid actFlag {document.ActFlag}";
                return false;
            }

            if (!TryDecodeBitmask(document.FogBitmask, out var bitmask))
            {
                error = "fogBitmask is not valid base64";
                return false;
            }

            if (!Enum.TryParse(document.DamarisBranch ?? nameof(DamarisChoice.Undecided), true, out DamarisChoice branch) || !Enum.IsDefined(typeof(DamarisChoice), branch))
            {
                error = $"invalid damarisBranch {document.DamarisBranch}";
                return false;
            }

            if (document.PlaytimeSeconds < 0)
            {
                error = $"invalid playtimeSeconds {document.PlaytimeSeconds}";
                return false;
            }

            save = new SaveData
            {
                ActFlag = (Act)document.ActFlag,
                FogBitmask = bitmask,
                DamarisBranch = branch,
                Reputation = document.Reputation,
                Position = new Coord(document.Position?.X ?? 0, document.Position?.Y ?? 0),
                Location = document.Location ?? string.Empty,
                PlaytimeSeconds = document.PlaytimeSeconds,
                Timestamp = document.Timestamp
            };

            foreach (var pair in document.Inventory ?? new Dictionary<string, int>())
            {
                save.Inventory[pair.Key] = pair.Value;
            }

            foreach (var pair in document.Duels ?? new Dictionary<string, DuelDocument>())
            {
                if (!Enum.TryParse(pair.Value.Victory, true, out DuelVictory victory) || !Enum.IsDefined(typeof(DuelVictory), victory))
                {
                    error = $"invalid victory '{pair.Value.Victory}' for duel {pair.Key}";
                    save = null;
                    return false;
                }

                save.Duels[pair.Key] = new DuelRecord(pair.Value.Won, victory);
            }

            foreach (var pair in document.Flags ?? new Dictionary<string, string>())
            {
                save.Flags[pair.Key] = pair.Value;
            }

            error = null;
            return true;
        }

        private static bool TryDecodeBitmask(string encoded, out byte[] bitmask)
        {
            bitmask = Array.Empty<byte>();
            if (string.IsNullOrEmpty(encoded))
            {
                return true;
            }

            try
            {
                bitmask = Convert.FromBase64String(encoded);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private sealed class SaveDocument
        {
            [JsonProperty("actFlag")] public int ActFlag { get; set; } = (int)Act.One;
            [JsonProperty("fogBitmask")] public string FogBitmask { get; set; }
            [JsonProperty("damarisBranch")] public string DamarisBranch { get; set; }
            [JsonProperty("reputation")] public int Reputation { get; set; }
            [JsonProperty("inventory")] public Dictionary<string, int> Inventory { get; set; }
            [JsonProperty("position")] public PositionDocument Position { get; set; }
            [JsonProperty("location")] public string Location { get; set; }
            [JsonProperty("playtimeSeconds")] public long PlaytimeSeconds { get; set; }
            [JsonProperty("timestamp")] public long Timestamp { get; set; }
            [JsonProperty("duels")] public Dictionary<string, DuelDocument> Duels { get; set; }
            [JsonProperty("flags")] public Dictionary<string, string> Flags { get; set; }
        }

        private sealed class PositionDocument
        {
            [JsonProperty("x")] public int X { get; set; }
            [JsonProperty("y")] public int Y { get; set; }
        }

        private sealed class DuelDocument
        {
            [JsonProperty("won")] public bool Won { get; set; }
            [JsonProperty("victory")] public string Victory { get; set; }
        }
    }
}
