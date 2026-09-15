using System;
using Game.Core;

namespace Game.Grid
{
    public readonly struct ExplorationSnapshot
    {
        public readonly Coord PlayerCell;
        public readonly Facing Facing;
        private readonly byte[] fogBitmask;

        public ExplorationSnapshot(Coord playerCell, Facing facing, byte[] fogBitmask)
        {
            PlayerCell = playerCell;
            Facing = facing;
            this.fogBitmask = fogBitmask == null ? Array.Empty<byte>() : (byte[])fogBitmask.Clone();
        }

        public byte[] CopyFogBitmask() => (byte[])(fogBitmask ?? Array.Empty<byte>()).Clone();
    }
}
