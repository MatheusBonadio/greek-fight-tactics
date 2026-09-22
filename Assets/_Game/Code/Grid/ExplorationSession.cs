using System;
using Game.Core;

namespace Game.Grid
{
    public sealed class ExplorationSession : IDisposable
    {
        private readonly GridWalker walker;
        private readonly FogOfWar fog;
        private readonly int revealRadius;

        public ExplorationSession(GridWalker walker, FogOfWar fog, int revealRadius = FogOfWar.DefaultRevealRadius)
        {
            this.walker = walker ?? throw new ArgumentNullException(nameof(walker));
            this.fog = fog ?? throw new ArgumentNullException(nameof(fog));
            this.revealRadius = revealRadius;

            walker.CellEntered += OnCellEntered;
            fog.Reveal(walker.Cell, revealRadius);
        }

        public event Action<int> FogRevealed;

        public ExplorationSnapshot Capture() => new(walker.Cell, walker.Facing, fog.ToBitmask());

        public bool TryRestore(ExplorationSnapshot snapshot)
        {
            if (!walker.TryFindFreeCellNear(snapshot.PlayerCell, out var cell) || !fog.TryLoadBitmask(snapshot.CopyFogBitmask()))
            {
                return false;
            }

            walker.Teleport(cell, snapshot.Facing);
            return true;
        }

        public bool TryMergeFog(byte[] bitmask)
        {
            var current = fog.ToBitmask();
            if (bitmask == null || bitmask.Length != current.Length)
            {
                return false;
            }

            for (var index = 0; index < current.Length; index++)
            {
                current[index] |= bitmask[index];
            }

            return fog.TryLoadBitmask(current);
        }

        public void Dispose()
        {
            walker.CellEntered -= OnCellEntered;
        }

        private void OnCellEntered(Coord cell)
        {
            var revealed = fog.Reveal(cell, revealRadius);
            if (revealed > 0)
            {
                FogRevealed?.Invoke(revealed);
            }
        }
    }
}
