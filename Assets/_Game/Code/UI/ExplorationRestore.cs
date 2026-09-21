using System;
using Game.Core;
using Game.Grid;

namespace Game.UI
{
    public static class ExplorationRestore
    {
        public static bool TryApply(ExplorationSession session, SaveData save, string locationId, Facing facing)
        {
            if (session == null || save == null || !string.Equals(save.Location, locationId, StringComparison.Ordinal))
            {
                return false;
            }

            return session.TryRestore(new ExplorationSnapshot(save.Position, facing, save.TryGetFog(locationId, out var fog) ? fog : save.FogBitmask));
        }

        public static bool TryMergeFog(ExplorationSession session, SaveData save, string locationId) =>
            session != null && save != null && save.TryGetFog(locationId, out var fog) && session.TryMergeFog(fog);
    }
}
