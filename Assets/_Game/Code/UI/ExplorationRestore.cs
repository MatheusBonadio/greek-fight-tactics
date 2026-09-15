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

            return session.TryRestore(new ExplorationSnapshot(save.Position, facing, save.FogBitmask));
        }
    }
}
