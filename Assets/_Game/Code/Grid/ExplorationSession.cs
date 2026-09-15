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

        public ExplorationSnapshot Capture() => new(walker.Cell, walker.Facing, fog.ToBitmask());

        public bool TryRestore(ExplorationSnapshot snapshot)
        {
            if (!fog.TryLoadBitmask(snapshot.CopyFogBitmask()))
            {
                return false;
            }

            walker.Teleport(snapshot.PlayerCell, snapshot.Facing);
            return true;
        }

        public void Dispose()
        {
            walker.CellEntered -= OnCellEntered;
        }

        private void OnCellEntered(Coord cell) => fog.Reveal(cell, revealRadius);
    }
}
