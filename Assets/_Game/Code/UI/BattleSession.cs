using System;
using System.Collections.Generic;
using System.Linq;
using Game.Battle;
using Game.Core;
using Game.Grid;

namespace Game.UI
{
    public sealed class BattleSetup
    {
        public int PlayerId { get; set; }

        public Coord PlayerCell { get; set; }

        public Facing PlayerFacing { get; set; } = Facing.NorthEast;

        public int OpponentId { get; set; }

        public Coord OpponentCell { get; set; }

        public Facing OpponentFacing { get; set; } = Facing.SouthWest;

        public Profile OpponentProfile { get; set; } = Profile.Sophist;

        public int OpponentMaxCoherence { get; set; } = BattleArena.OpponentMaxCoherence;

        public int InitialAudience { get; set; } = BattleRules.MaxAudience / 2;

        public Act Act { get; set; } = Act.One;

        public IEnumerable<int> AudienceIds { get; set; } = Array.Empty<int>();

        public IEnumerable<ActionId> LockedActions { get; set; } = Array.Empty<ActionId>();

        public IEnumerable<AbilityId> Abilities { get; set; } = Array.Empty<AbilityId>();

        public IEnumerable<KeyValuePair<int, Coord>> Spectators { get; set; } = Array.Empty<KeyValuePair<int, Coord>>();

        public int Seed { get; set; }

        public BattleRules Rules { get; set; }
    }

    public sealed class BattleSession
    {
        public const int PlayerMaxCoherence = BattleArena.PlayerMaxCoherence;
        public const int PlayerInitiative = 5;
        public const int OpponentInitiative = 10;
        public const int StarterQuoteCoherence = 10;
        public const int StarterQuoteAudience = 5;
        public const int StarterQuoteUses = 1;

        private readonly BattleResolver resolver = new();
        private readonly PathFinder pathFinder = new();
        private readonly List<Coord> reachable = new();
        private readonly IOpponentPolicy opponentPolicy;
        private readonly IArgumentSource arguments;

        private BattleSession(BattleState state, IArgumentSource arguments, IOpponentPolicy opponentPolicy)
        {
            State = state;
            this.arguments = arguments;
            this.opponentPolicy = opponentPolicy;
            Menu = new ActionMenuModel(state, resolver);
        }

        public BattleState State { get; }

        public ActionMenuModel Menu { get; }

        public bool IsOver => State.Status != BattleStatus.Ongoing;

        public bool IsPlayerTurn => !IsOver && State.Queue.Current == State.Player.Id;

        public static GridModel CopyForBattle(GridModel exploration, IEnumerable<int> debaterIds, IEnumerable<KeyValuePair<int, Coord>> spectators = null)
        {
            var copy = CopyWithout(exploration, debaterIds);
            foreach (var (spectatorId, cell) in spectators ?? Enumerable.Empty<KeyValuePair<int, Coord>>())
            {
                if (spectatorId != TileData.NoOccupant && copy.TryGet(cell, out var tile) && tile.IsPassable && !tile.IsOccupied)
                {
                    tile.OccupantId = spectatorId;
                    copy.TrySet(cell, tile);
                }
            }

            return copy;
        }

        private static GridModel CopyWithout(GridModel exploration, IEnumerable<int> debaterIds)
        {
            if (exploration == null)
            {
                throw new ArgumentNullException(nameof(exploration));
            }

            var ids = new HashSet<int>(debaterIds ?? Enumerable.Empty<int>());
            var copy = new GridModel(exploration.Width, exploration.Height);
            for (var y = 0; y < exploration.Height; y++)
            {
                for (var x = 0; x < exploration.Width; x++)
                {
                    var cell = new Coord(x, y);
                    var tile = exploration[cell];
                    if (ids.Contains(tile.OccupantId))
                    {
                        tile.OccupantId = TileData.NoOccupant;
                    }

                    copy.TrySet(cell, tile);
                }
            }

            return copy;
        }

        public static BattleSession Start(GridModel exploration, BattleSetup setup, IArgumentSource arguments, IOpponentPolicy opponentPolicy = null)
        {
            if (setup == null)
            {
                throw new ArgumentNullException(nameof(setup));
            }

            var spectators = (setup.Spectators ?? Enumerable.Empty<KeyValuePair<int, Coord>>())
                .Where(spectator => spectator.Value != setup.PlayerCell && spectator.Value != setup.OpponentCell);
            var grid = CopyForBattle(exploration, new[] { setup.PlayerId, setup.OpponentId }, spectators);
            var player = new Debater(setup.PlayerId, Side.Player, Profile.Philosopher, PlayerMaxCoherence, PlayerInitiative, setup.PlayerCell, setup.PlayerFacing);
            var opponent = new Debater(setup.OpponentId, Side.Opponent, setup.OpponentProfile, setup.OpponentMaxCoherence, OpponentInitiative, setup.OpponentCell, setup.OpponentFacing);
            var state = new BattleState(
                grid,
                setup.Act,
                setup.Rules ?? new BattleRules(),
                new[] { player, opponent },
                setup.InitialAudience,
                new[] { new QuoteItem(BattleArena.StarterQuoteId, StarterQuoteCoherence, StarterQuoteAudience, StarterQuoteUses) },
                setup.AudienceIds,
                setup.LockedActions,
                setup.Abilities);

            return new BattleSession(state, arguments, opponentPolicy ?? new OpponentBrain(new Random(setup.Seed)));
        }

        public Outcome PlayOpponentTurn()
        {
            var self = State.Find(State.Queue.Current) ?? State.Opponent;
            return Commit(opponentPolicy.Decide(State, self, arguments));
        }

        public Outcome PlayChoice(BattleMenuChoice choice) => Commit(ToPlay(choice));

        public Play ToPlay(BattleMenuChoice choice)
        {
            var player = State.Player;
            var opponent = State.Opponent;
            return choice.Action switch
            {
                ActionId.Circulate => Play.Circulate(player.Id, choice.Destination ?? CirculateDestination()),
                ActionId.Refute => Play.Refute(player.Id, opponent.Id, choice.FallacyId),
                ActionId.Maieutics => Play.Maieutics(player.Id, opponent.Id, choice.PremiseIndex),
                ActionId.Quote => Play.Quote(player.Id, State.QuoteIds.FirstOrDefault(id => State.TryGetQuote(id, out _, out var uses) && uses > 0)),
                ActionId.Concede => Play.Concede(player.Id),
                _ => Play.Withdraw(player.Id)
            };
        }

        public Coord CirculateDestination()
        {
            var player = State.Player;
            var target = State.Opponent.Position;
            pathFinder.Reachable(State.Grid, player.Position, State.Rules.MoveRange, reachable);

            var best = player.Position;
            var bestDistance = int.MaxValue;
            foreach (var cell in reachable)
            {
                var isCurrent = cell.X == player.Position.X && cell.Y == player.Position.Y;
                var distance = Math.Abs(cell.X - target.X) + Math.Abs(cell.Y - target.Y);
                if (!isCurrent && distance < bestDistance)
                {
                    best = cell;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private Outcome Commit(Play play)
        {
            var outcome = resolver.Resolve(play, State);
            State.Apply(outcome);
            Menu.Refresh();
            return outcome;
        }
    }
}
