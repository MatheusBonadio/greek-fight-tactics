using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode.Core
{
    public sealed class GameStateMachineTests
    {
        [Test]
        public void BattleLoop_FollowsTheIntendedFlow()
        {
            var machine = new GameStateMachine();
            var visited = new List<GameMode> { machine.Mode };
            machine.Changed += (_, to) => visited.Add(to);

            Assert.IsTrue(machine.TryTransition(GameMode.BattleIntro));
            Assert.IsTrue(machine.TryTransition(GameMode.BattleTurn));
            Assert.IsTrue(machine.TryTransition(GameMode.BattleResolve));
            Assert.IsTrue(machine.TryTransition(GameMode.BattleTurn));
            Assert.IsTrue(machine.TryTransition(GameMode.BattleResolve));
            Assert.IsTrue(machine.TryTransition(GameMode.BattleOutro));
            Assert.IsTrue(machine.TryTransition(GameMode.Exploration));

            CollectionAssert.AreEqual(
                new[] { GameMode.Exploration, GameMode.BattleIntro, GameMode.BattleTurn, GameMode.BattleResolve, GameMode.BattleTurn, GameMode.BattleResolve, GameMode.BattleOutro, GameMode.Exploration },
                visited);
        }

        [TestCase(GameMode.Exploration, GameMode.BattleTurn)]
        [TestCase(GameMode.BattleTurn, GameMode.Exploration)]
        [TestCase(GameMode.BattleIntro, GameMode.BattleOutro)]
        [TestCase(GameMode.BattleOutro, GameMode.BattleTurn)]
        public void IllegalTransitions_AreRefused(GameMode from, GameMode to)
        {
            Assert.IsFalse(GameStateMachine.CanTransition(from, to));
        }

        [Test]
        public void ExplorationInput_IsFrozenDuringBattle()
        {
            var machine = new GameStateMachine();
            Assert.IsTrue(machine.IsExplorationInputEnabled);

            machine.TryTransition(GameMode.BattleIntro);

            Assert.IsTrue(machine.IsInBattle);
            Assert.IsFalse(machine.IsExplorationInputEnabled);
            Assert.IsFalse(machine.IsBattleInputEnabled);

            machine.TryTransition(GameMode.BattleTurn);
            Assert.IsTrue(machine.IsBattleInputEnabled);
        }

        [Test]
        public void Overlays_ReturnToTheModeTheyInterrupted()
        {
            var machine = new GameStateMachine();
            machine.TryTransition(GameMode.BattleIntro);
            machine.TryTransition(GameMode.BattleTurn);

            Assert.IsTrue(machine.TryOpenOverlay(GameMode.Dialogue));
            Assert.IsFalse(machine.IsBattleInputEnabled);
            Assert.IsTrue(machine.TryOpenOverlay(GameMode.Paused));
            Assert.IsFalse(machine.TryOpenOverlay(GameMode.Paused));
            Assert.IsFalse(machine.TryTransition(GameMode.BattleResolve));

            Assert.IsTrue(machine.TryCloseOverlay());
            Assert.AreEqual(GameMode.Dialogue, machine.Mode);
            Assert.IsTrue(machine.TryCloseOverlay());
            Assert.AreEqual(GameMode.BattleTurn, machine.Mode);
            Assert.IsFalse(machine.TryCloseOverlay());
        }

        [TestCase(3, 0, true)]
        [TestCase(2, 1, true)]
        [TestCase(2, 2, false)]
        [TestCase(0, 4, false)]
        public void Aggro_TriggersInsideTheRadius(int offsetX, int offsetY, bool triggered)
        {
            var player = new Coord(5, 5);

            Assert.AreEqual(triggered, AggroRule.IsTriggered(player, new Coord(5 + offsetX, 5 + offsetY)));
        }
    }
}
