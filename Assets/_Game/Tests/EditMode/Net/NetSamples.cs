using Game.Core;

namespace Game.Tests.EditMode.Net
{
    internal static class NetSamples
    {
        public static SaveData SampleSave(long timestamp = 1789480000000, int reputation = 12)
        {
            var save = new SaveData
            {
                ActFlag = Act.Two,
                FogBitmask = new byte[] { 1, 2, 255 },
                DamarisBranch = DamarisChoice.Accepted,
                Reputation = reputation,
                Position = new Coord(7, 3),
                Location = "agora",
                PlaytimeSeconds = 3725,
                Timestamp = timestamp
            };
            save.Inventory["heraclitus"] = 2;
            save.Duels["batilo"] = new DuelRecord(true, DuelVictory.Maieutics);
            save.Flags["met_menexeno"] = "yes";
            return save;
        }
    }
}
