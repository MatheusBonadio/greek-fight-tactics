using Game.Core;
using Game.Grid;
using Unity.Scripting.LifecycleManagement;

namespace Game.UI
{
    public readonly struct PendingArrival
    {
        public readonly string LocationId;
        public readonly Coord Cell;
        public readonly Facing Facing;
        public readonly string DepartureLocationId;
        public readonly byte[] DepartureFog;

        public PendingArrival(string locationId, Coord cell, Facing facing, string departureLocationId = null, byte[] departureFog = null)
        {
            LocationId = locationId;
            Cell = cell;
            Facing = facing;
            DepartureLocationId = departureLocationId;
            DepartureFog = departureFog;
        }
    }

    public static partial class SceneArrival
    {
        [AutoStaticsCleanup]
        private static PendingArrival? pending;

        public static bool HasPending => pending.HasValue;

        public static void Set(PendingArrival arrival)
        {
            pending = arrival;
        }

        public static bool TryConsume(string locationId, out PendingArrival arrival)
        {
            arrival = default;
            if (!pending.HasValue || pending.Value.LocationId != locationId)
            {
                return false;
            }

            arrival = pending.Value;
            pending = null;
            return true;
        }

        public static void Clear()
        {
            pending = null;
        }
    }
}
