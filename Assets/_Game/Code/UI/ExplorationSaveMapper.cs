using Game.Core;
using Game.Grid;

namespace Game.UI
{
    public static class ExplorationSaveMapper
    {
        public static SaveData ToSave(ExplorationSnapshot snapshot, SaveData previous, long timestamp)
        {
            var save = new SaveData
            {
                ActFlag = previous?.ActFlag ?? Act.One,
                DamarisBranch = previous?.DamarisBranch ?? DamarisChoice.Undecided,
                Reputation = previous?.Reputation ?? 0,
                Position = snapshot.PlayerCell,
                FogBitmask = snapshot.CopyFogBitmask(),
                Timestamp = timestamp
            };

            if (previous == null)
            {
                return save;
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

            return save;
        }
    }
}
