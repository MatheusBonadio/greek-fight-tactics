using System.Collections;
using System.Linq;
using Game.Core;
using Game.Grid;
using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.PlayMode
{
    internal static class DuelChallenge
    {
        private static readonly Coord[] Sides = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

        public static void Confirm(GridSandboxController sandbox, Coord cell)
        {
            Assert.IsTrue(sandbox.Cursor.TryPoint(cell), $"the cursor cannot point at {cell}");
            sandbox.ConfirmCursor();
        }

        public static bool TryFindFreeCellBeside(GridSandboxController sandbox, Coord target, out Coord free)
        {
            var player = sandbox.Player.Walker;
            foreach (var side in Sides)
            {
                var cell = target + side;
                var isFree = sandbox.Grid.TryGet(cell, out var tile) && tile.IsPassable && (!tile.IsOccupied || tile.OccupantId == player.ActorId);
                if (isFree && PathFinder.AreBesideOnFoot(sandbox.Grid, cell, target) && sandbox.Exits.All(exit => exit.Cell != cell))
                {
                    free = cell;
                    return true;
                }
            }

            free = target;
            return false;
        }

        public static void StandBeside(GridSandboxController sandbox, MapOpponent entry)
        {
            var player = sandbox.Player.Walker;
            var target = entry.Actor.Walker.Cell;
            if (!PathFinder.AreBesideOnFoot(sandbox.Grid, player.Cell, target))
            {
                Assert.IsTrue(TryFindFreeCellBeside(sandbox, target, out var beside), $"nobody can stand beside {entry.Opponent.Id}");
                player.Teleport(beside, Facing.NorthEast);
            }

            new FogOfWar(sandbox.Grid).Reveal(player.Cell);
        }

        public static IEnumerator Engage(GridSandboxController sandbox, SandboxBattleDirector director, MapOpponent entry, float timeoutSeconds)
        {
            StandBeside(sandbox, entry);
            yield return null;
            Confirm(sandbox, entry.Actor.Walker.Cell);
            yield return WaitFor(() => director.Phase != BattleFlowPhase.Exploring, timeoutSeconds);
            Assert.AreSame(entry, director.Engaged, $"confirming on {entry.Opponent.Id} did not start the duel");
        }

        public static IEnumerator WaitFor(System.Func<bool> condition, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }
    }
}
