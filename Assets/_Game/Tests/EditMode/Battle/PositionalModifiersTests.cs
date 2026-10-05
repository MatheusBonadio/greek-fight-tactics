using Game.Battle;
using Game.Core;
using Game.Grid;
using Game.Tests.EditMode.Grid;
using NUnit.Framework;
using static Game.Tests.EditMode.Battle.BattleFactory;

namespace Game.Tests.EditMode.Battle
{
    public sealed class PositionalModifiersTests
    {
        private BattleResolver resolver;

        [SetUp]
        public void SetUp()
        {
            resolver = new BattleResolver();
        }

        [Test]
        public void AdjacentCitizens_AddAudienceToRefutation()
        {
            var plain = CreateWithArgument(SoundArgument());
            var crowded = CreateWithArgumentAndCitizens(2);

            var plainOutcome = resolver.Resolve(Play.Refute(PlayerId, OpponentId, Fallacy), plain);
            var crowdedOutcome = resolver.Resolve(Play.Refute(PlayerId, OpponentId, Fallacy), crowded);

            Assert.AreEqual(2, PositionalModifiers.AdjacentAudienceMembers(crowded, PlayerStart));
            Assert.AreEqual(2 * crowded.Rules.CitizenAdjacencyAudienceBonus, crowdedOutcome.PlayerAudienceDelta - plainOutcome.PlayerAudienceDelta);
        }

        [Test]
        public void Cornered_LosesCoherenceOnAnyAction()
        {
            var state = CreateWithArgument(RefutableArgument());
            GridFactory.Block(state.Grid, PlayerStart + new Coord(-1, 0));
            GridFactory.Block(state.Grid, PlayerStart + new Coord(0, 1));
            GridFactory.Block(state.Grid, PlayerStart + new Coord(0, -1));

            var outcome = resolver.Resolve(Play.Concede(PlayerId), state);

            Assert.IsTrue(PositionalModifiers.IsCornered(state, PlayerStart));
            Assert.AreEqual(state.Rules.ConcedeRecovery - state.Rules.CorneredCoherencePenalty, outcome.ActorCoherenceDelta);
        }

        [Test]
        public void TwoBlockedNeighbours_AreNotCornered()
        {
            var state = Create();
            GridFactory.Block(state.Grid, PlayerStart + new Coord(-1, 0));
            GridFactory.Block(state.Grid, PlayerStart + new Coord(0, 1));

            Assert.IsFalse(PositionalModifiers.IsCornered(state, PlayerStart));
        }

        [Test]
        public void RefutingFromBehind_AddsFlankBonus()
        {
            var front = CreateWithArgument(SoundArgument());
            var behind = CreateWithArgument(SoundArgument(), opponentFacing: Facing.NorthEast);

            var frontOutcome = resolver.Resolve(Play.Refute(PlayerId, OpponentId, Fallacy), front);
            var behindOutcome = resolver.Resolve(Play.Refute(PlayerId, OpponentId, Fallacy), behind);

            Assert.IsFalse(PositionalModifiers.IsFlanking(front.Player, front.Opponent));
            Assert.IsTrue(PositionalModifiers.IsFlanking(behind.Player, behind.Opponent));
            Assert.AreEqual(-behind.Rules.FlankRefuteBonus, behindOutcome.TargetCoherenceDelta - frontOutcome.TargetCoherenceDelta);
        }

        [Test]
        public void DiagramTile_StrengthensMaieutics()
        {
            var plain = CreateWithArgument(RefutableArgument());
            var diagram = CreateWithArgument(RefutableArgument());
            var tile = diagram.Grid[PlayerStart];
            tile.Terrain = TerrainId.Diagram;
            diagram.Grid.TrySet(PlayerStart, tile);

            var plainOutcome = resolver.Resolve(Play.Maieutics(PlayerId, OpponentId, WeakPremise), plain);
            var diagramOutcome = resolver.Resolve(Play.Maieutics(PlayerId, OpponentId, WeakPremise), diagram);

            Assert.AreEqual(-diagram.Rules.DiagramMaieuticsBonus, diagramOutcome.TargetCoherenceDelta - plainOutcome.TargetCoherenceDelta);
        }

        private static BattleState CreateWithArgumentAndCitizens(int citizens)
        {
            var state = Create(playerInitiative: 5, opponentInitiative: 10, audienceMembers: new[] { CitizenId, CitizenId + 1 });
            var neighbours = new[] { PlayerStart + new Coord(0, 1), PlayerStart + new Coord(0, -1) };
            for (var index = 0; index < citizens; index++)
            {
                GridFactory.Occupy(state.Grid, neighbours[index], CitizenId + index);
            }

            state.Apply(new BattleResolver().Resolve(Play.Argue(OpponentId, PlayerId, SoundArgument()), state));
            return state;
        }
    }
}
