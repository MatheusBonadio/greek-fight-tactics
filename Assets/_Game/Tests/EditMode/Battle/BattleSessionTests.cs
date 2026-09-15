using System.Linq;
using Game.Battle;
using Game.Content;
using Game.Core;
using Game.EditorTools;
using Game.Grid;
using Game.Tests.EditMode.Grid;
using Game.UI;
using NUnit.Framework;
using UnityEditor;
using static Game.Tests.EditMode.Battle.BattleFactory;

namespace Game.Tests.EditMode.Battle
{
    public sealed class BattleSessionTests
    {
        private const int PlayerActor = 1;
        private const int CitizenActor = 3;
        private const int OpponentActor = 5;
        private const int MaxExchanges = 200;

        [Test]
        public void Start_BattlesOnACopyAndLeavesTheExplorationGridToTheWalkers()
        {
            var (grid, session) = StartSession();

            Assert.AreNotSame(grid, session.State.Grid);
            Assert.AreEqual(PlayerActor, grid[new Coord(2, 2)].OccupantId);
            Assert.AreEqual(OpponentActor, grid[new Coord(4, 3)].OccupantId);
            Assert.AreEqual(CitizenActor, session.State.Grid[new Coord(3, 5)].OccupantId);
            Assert.IsTrue(session.State.IsAudienceMember(CitizenActor));
            Assert.IsFalse(session.IsPlayerTurn);

            session.PlayChoice(new BattleMenuChoice(ActionId.Concede));
            Assert.AreEqual(PlayerActor, grid[new Coord(2, 2)].OccupantId);
        }

        [Test]
        public void OpponentArguesAndTheRightRefutationHits()
        {
            var (_, session) = StartSession();

            for (var exchange = 0; exchange < MaxExchanges && !(session.IsPlayerTurn && session.State.CurrentArgument.HasValue); exchange++)
            {
                if (session.IsPlayerTurn)
                {
                    session.PlayChoice(new BattleMenuChoice(ActionId.Concede));
                }
                else
                {
                    session.PlayOpponentTurn();
                }
            }

            Assert.IsTrue(session.State.CurrentArgument.HasValue);
            Assume.That(session.Menu.Entries.First(entry => entry.Action == ActionId.Refute).IsAvailable);

            var before = session.State.Opponent.Coherence;
            var outcome = session.PlayChoice(new BattleMenuChoice(ActionId.Refute, session.State.CurrentArgument.Value.FallacyId));

            Assert.IsTrue(outcome.Hit);
            Assert.Less(session.State.Opponent.Coherence, before);
        }

        [Test]
        public void Circulate_StepsTowardTheOpponent()
        {
            var grid = GridFactory.Open(12, 12);
            new GridWalker(grid, PlayerActor, new Coord(0, 0));
            new GridWalker(grid, OpponentActor, new Coord(9, 9));
            var session = BattleSession.Start(grid, new BattleSetup { PlayerId = PlayerActor, PlayerCell = new Coord(0, 0), OpponentId = OpponentActor, OpponentCell = new Coord(9, 9) }, Source());

            var destination = session.CirculateDestination();

            Assert.Less(destination.X + destination.Y, 18);
            Assert.Greater(destination.X + destination.Y, 0);
        }

        [Test]
        public void AFullBattle_EndsWithAResult()
        {
            var (_, session) = StartSession();

            for (var exchange = 0; exchange < MaxExchanges && !session.IsOver; exchange++)
            {
                if (!session.IsPlayerTurn)
                {
                    session.PlayOpponentTurn();
                    continue;
                }

                session.PlayChoice(PickChoice(session));
            }

            Assert.IsTrue(session.IsOver);
            Assert.AreNotEqual(BattleStatus.Ongoing, session.State.Status);
        }

        [Test]
        public void GeneratedThrasybios_HasAnIdAndAPoolOfRefutableFallacies()
        {
            var opponent = AssetDatabase.LoadAssetAtPath<OpponentSO>(DataTools.OpponentPath(SandboxTools.BattleOpponentId));

            Assert.IsNotNull(opponent, "run DataTools.Regenerate to create the opponent assets");
            Assert.AreEqual(SandboxTools.BattleOpponentId, opponent.Id);
            Assert.AreEqual(Profile.Sophist, opponent.Profile);
            Assert.IsNotEmpty(opponent.FallacyPool);
            Assert.IsTrue(opponent.FallacyPool.All(fallacy => fallacy.IsRefutable));
            Assert.AreEqual(SandboxTools.BattleOpponentId, Game.Net.OpponentContext.From(opponent).Id);
        }

        [Test]
        public void Circulate_GoesToTheChosenDestinationWhenOneIsGiven()
        {
            var (_, session) = StartSession();
            var chosen = new Coord(2, 1);

            var play = session.ToPlay(new BattleMenuChoice(ActionId.Circulate, destination: chosen));

            Assert.AreEqual(ActionId.Circulate, play.Action);
            Assert.AreEqual(chosen, play.Destination);
        }

        [Test]
        public void CirculateTargeting_AcceptsOnlyHighlightedMoveCellsOtherThanThePlayer()
        {
            var (_, session) = StartSession();
            var state = session.State;
            for (var y = 0; y < state.Grid.Height; y++)
            {
                for (var x = 0; x < state.Grid.Width; x++)
                {
                    var tile = state.Grid[new Coord(x, y)];
                    tile.Revealed = true;
                    state.Grid.TrySet(new Coord(x, y), tile);
                }
            }

            var highlighter = new RangeHighlighter(state.Grid, new PathFinder());
            var player = state.Player.Position;
            bool IsValid(Coord cell) => CirculateTargeting.IsValidDestination(highlighter, state, cell);

            Assert.IsFalse(IsValid(new Coord(2, 3)), "nothing is highlighted yet");

            highlighter.ShowMove(player, state.Rules.MoveRange);
            var reachable = highlighter.Cells.First(cell => cell != player);
            Assert.IsTrue(IsValid(reachable));
            Assert.IsFalse(IsValid(player));
            Assert.IsFalse(IsValid(new Coord(11, 11)));

            highlighter.ShowRefute(player, state.Rules.RefuteRange);
            Assert.IsFalse(IsValid(reachable), "only the move range accepts a destination");
        }

        internal static BattleMenuChoice PickChoice(BattleSession session)
        {
            bool Available(ActionId action) => session.Menu.Entries.Any(entry => entry.Action == action && entry.IsAvailable);

            var argument = session.State.CurrentArgument;
            if (argument.HasValue && argument.Value.IsRefutable && Available(ActionId.Refute))
            {
                return new BattleMenuChoice(ActionId.Refute, argument.Value.FallacyId);
            }

            if (argument.HasValue && Available(ActionId.Maieutics))
            {
                return new BattleMenuChoice(ActionId.Maieutics, premiseIndex: argument.Value.WeakPremise);
            }

            return Available(ActionId.Circulate) ? new BattleMenuChoice(ActionId.Circulate) : new BattleMenuChoice(ActionId.Concede);
        }

        private static (GridModel Grid, BattleSession Session) StartSession()
        {
            var grid = GridFactory.Open(12, 12);
            new GridWalker(grid, PlayerActor, new Coord(2, 2));
            new GridWalker(grid, OpponentActor, new Coord(4, 3));
            new GridWalker(grid, CitizenActor, new Coord(3, 5));

            var session = BattleSession.Start(grid, new BattleSetup
            {
                PlayerId = PlayerActor,
                PlayerCell = new Coord(2, 2),
                OpponentId = OpponentActor,
                OpponentCell = new Coord(4, 3),
                AudienceIds = new[] { CitizenActor },
                Seed = 402
            }, Source());

            return (grid, session);
        }

        private static IArgumentSource Source() => new ListArgumentSource(new[] { RefutableArgument() }, 402);
    }
}
