using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Grid;

namespace Game.UI
{
    public static class ExplorationSaveMapper
    {
        public static SaveData ToSave(ExplorationSnapshot snapshot, SaveData previous, long timestamp, string locationId, long playtimeSeconds, IEnumerable<string> unlockedFallacies = null, DuelProgress duels = null)
        {
            var save = new SaveData
            {
                ActFlag = previous?.ActFlag ?? Act.One,
                DamarisBranch = previous?.DamarisBranch ?? DamarisChoice.Undecided,
                Reputation = previous?.Reputation ?? 0,
                Position = snapshot.PlayerCell,
                FogBitmask = snapshot.CopyFogBitmask(),
                Location = locationId ?? string.Empty,
                PlaytimeSeconds = Math.Max(0, playtimeSeconds),
                Timestamp = timestamp
            };

            if (previous != null)
            {
                CopyProgress(previous, save);
            }

            duels?.WriteTo(save);
            if (!string.IsNullOrEmpty(save.Location))
            {
                save.FogByLocation[save.Location] = save.FogBitmask;
            }

            save.Compendium.UnionWith((unlockedFallacies ?? Enumerable.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)));
            return save;
        }

        private static void CopyProgress(SaveData previous, SaveData save)
        {
            if (previous.TryGetFog(previous.Location, out var previousFog))
            {
                save.FogByLocation[previous.Location] = previousFog;
            }

            foreach (var pair in previous.FogByLocation)
            {
                save.FogByLocation[pair.Key] = pair.Value;
            }

            foreach (var pair in previous.Inventory)
            {
                save.Inventory[pair.Key] = pair.Value;
            }

            foreach (var pair in previous.Duels)
            {
                save.Duels[pair.Key] = pair.Value;
            }

            foreach (var pair in previous.Flags)
            {
                save.Flags[pair.Key] = pair.Value;
            }

            save.Compendium.UnionWith(previous.Compendium);
        }
    }
}
