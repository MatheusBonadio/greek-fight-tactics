using System.Collections.Generic;
using Game.Battle;
using Game.Core;
using Game.Grid;
using Game.Tests.EditMode.Grid;

namespace Game.Tests.EditMode.Battle
{
    internal static class BattleFactory
    {
        public const int PlayerId = 1;
        public const int OpponentId = 2;
        public const int CitizenId = 100;
        public const string Fallacy = "ad_hominem";
        public const string QuoteId = "heraclitus";
        public const int WeakPremise = 1;

        public static readonly Coord PlayerStart = new(2, 2);
        public static readonly Coord OpponentStart = new(4, 2);

        public static Argument RefutableArgument() =>
            new("Quem nasceu no Cerâmico não tem autoridade para falar de justiça na ágora.", Fallacy, new[] { "Kleia é do Cerâmico", "Oleiros não entendem de justiça" }, WeakPremise, ArgumentTones.Mocking);

        public static Argument Paradox() =>
            new("Para cruzar a ágora você precisa cruzar a metade dela, e antes a metade da metade.", null, new[] { "Toda distância tem uma metade", "Infinitos passos não terminam" }, WeakPremise, ArgumentTones.Calm);

        public static BattleState Create(
            Act act = Act.One,
            Profile opponentProfile = Profile.Sophist,
            int playerInitiative = 10,
            int opponentInitiative = 5,
            int initialPlayerAudience = 40,
            int size = 8,
            IEnumerable<int> audienceMembers = null,
            BattleRules rules = null,
            Facing opponentFacing = Facing.SouthWest,
            IEnumerable<ActionId> lockedActions = null,
            IEnumerable<AbilityId> abilities = null)
        {
            var grid = GridFactory.Open(size, size);
            new FogOfWar(grid).Reveal(new Coord(size / 2, size / 2), size * 2);
            var player = new Debater(PlayerId, Side.Player, Profile.Philosopher, 60, playerInitiative, PlayerStart, Facing.NorthEast);
            var opponent = new Debater(OpponentId, Side.Opponent, opponentProfile, 50, opponentInitiative, OpponentStart, opponentFacing);
            return new BattleState(
                grid,
                act,
                rules ?? new BattleRules(),
                new[] { player, opponent },
                initialPlayerAudience,
                new[] { new QuoteItem(QuoteId, 10, 5, 1) },
                audienceMembers,
                lockedActions,
                abilities);
        }

        public static BattleState CreateWithArgument(
            Argument argument,
            Act act = Act.One,
            Facing opponentFacing = Facing.SouthWest,
            Profile opponentProfile = Profile.Sophist,
            IEnumerable<int> audienceMembers = null,
            BattleRules rules = null,
            IEnumerable<ActionId> lockedActions = null)
        {
            var state = Create(act, opponentProfile, playerInitiative: 5, opponentInitiative: 10, audienceMembers: audienceMembers, rules: rules, opponentFacing: opponentFacing, lockedActions: lockedActions);
            var resolver = new BattleResolver();
            state.Apply(resolver.Resolve(Play.Argue(OpponentId, PlayerId, argument), state));
            return state;
        }
    }
}
