using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Grid;

namespace Game.Battle
{
    public sealed class BattleState
    {
        private readonly List<Debater> debaters;
        private readonly Dictionary<string, QuoteItem> quotes;
        private readonly Dictionary<string, int> quoteUses;
        private readonly HashSet<int> audienceMemberIds;
        private readonly HashSet<ActionId> lockedActions;
        private readonly Dictionary<AbilityId, int> abilityUses;
        private readonly List<BattleLogEntry> log = new();

        public BattleState(
            GridModel grid,
            Act act,
            BattleRules rules,
            IEnumerable<Debater> debaters,
            int initialPlayerAudience,
            IEnumerable<QuoteItem> quotes = null,
            IEnumerable<int> audienceMemberIds = null,
            IEnumerable<ActionId> lockedPlayerActions = null,
            IEnumerable<AbilityId> playerAbilities = null)
        {
            lockedActions = new HashSet<ActionId>(lockedPlayerActions ?? Enumerable.Empty<ActionId>());
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            abilityUses = (playerAbilities ?? Enumerable.Empty<AbilityId>()).Distinct().ToDictionary(ability => ability, ability => AbilityRules.UsesPerBattle(ability, Rules));
            Act = act;
            this.debaters = debaters?.ToList() ?? throw new ArgumentNullException(nameof(debaters));

            if (this.debaters.Count(debater => debater.Side == Side.Player) != 1 || this.debaters.All(debater => debater.Side != Side.Opponent))
            {
                throw new ArgumentException("A battle needs exactly one player and at least one opponent.", nameof(debaters));
            }

            foreach (var debater in this.debaters)
            {
                var tile = Grid[debater.Position];
                if (!Grid.Contains(debater.Position) || tile.IsOccupied)
                {
                    throw new ArgumentException($"Debater {debater.Id} starts outside the grid or on an occupied cell.", nameof(debaters));
                }

                tile.OccupantId = debater.Id;
                Grid.TrySet(debater.Position, tile);
            }

            this.quotes = (quotes ?? Enumerable.Empty<QuoteItem>()).ToDictionary(quote => quote.Id);
            quoteUses = this.quotes.Values.ToDictionary(quote => quote.Id, quote => quote.Uses);
            this.audienceMemberIds = new HashSet<int>(audienceMemberIds ?? Enumerable.Empty<int>());
            PlayerAudience = Math.Clamp(initialPlayerAudience, 0, BattleRules.MaxAudience);
            Queue = new TurnQueue(this.debaters);
        }

        public GridModel Grid { get; }

        public Act Act { get; }

        public BattleRules Rules { get; }

        public IReadOnlyList<Debater> Debaters => debaters;

        public Debater Player => debaters.First(debater => debater.Side == Side.Player);

        public Debater Opponent => debaters.First(debater => debater.Side == Side.Opponent);

        public TurnQueue Queue { get; }

        public int PlayerAudience { get; private set; }

        public int Turn { get; private set; }

        public BattleStatus Status { get; private set; } = BattleStatus.Ongoing;

        public int ReputationDelta { get; private set; }

        public Argument? CurrentArgument { get; private set; }

        public bool IsArgumentAnswered { get; private set; }

        public bool HasOpenArgument => CurrentArgument.HasValue && !IsArgumentAnswered;

        public IReadOnlyList<BattleLogEntry> Log => log;

        public int AudienceFor(Side side) => side == Side.Player ? PlayerAudience : BattleRules.MaxAudience - PlayerAudience;

        public Debater Find(int id) => debaters.FirstOrDefault(debater => debater.Id == id);

        public bool IsAudienceMember(int occupantId) => audienceMemberIds.Contains(occupantId);

        public IReadOnlyCollection<ActionId> LockedPlayerActions => lockedActions;

        public bool IsLockedForPlayer(ActionId action) => lockedActions.Contains(action);

        public IReadOnlyCollection<AbilityId> PlayerAbilities => abilityUses.Keys;

        public bool HasAbility(AbilityId ability) => abilityUses.ContainsKey(ability);

        public int AbilityUsesLeft(AbilityId ability) => abilityUses.TryGetValue(ability, out var left) ? left : 0;

        public bool CanUse(AbilityId ability) => AbilityUsesLeft(ability) > 0;

        public bool IsWeakPremiseRevealed { get; private set; }

        public IReadOnlyCollection<string> QuoteIds => quotes.Keys;

        public bool TryGetQuote(string id, out QuoteItem quote, out int usesLeft)
        {
            usesLeft = 0;
            if (id == null || !quotes.TryGetValue(id, out quote))
            {
                quote = default;
                return false;
            }

            usesLeft = quoteUses[id];
            return true;
        }

        public void Apply(Outcome outcome)
        {
            if (outcome == null)
            {
                throw new ArgumentNullException(nameof(outcome));
            }

            if (!outcome.IsRejected)
            {
                ApplyEffects(outcome);
                Turn++;
                Queue.Advance(outcome.TurnCost);
                Status = VictoryCheck.Evaluate(this);
            }

            log.Add(new BattleLogEntry(Turn, outcome, Player.Coherence, Opponent.Coherence, PlayerAudience, Status));
        }

        private void ApplyEffects(Outcome outcome)
        {
            var actor = Find(outcome.ActorId);
            ChangeCoherence(actor, outcome.ActorCoherenceDelta);
            ChangeCoherence(Find(outcome.TargetId), outcome.TargetCoherenceDelta);
            PlayerAudience = Math.Clamp(PlayerAudience + outcome.PlayerAudienceDelta, 0, BattleRules.MaxAudience);
            ReputationDelta += outcome.ReputationDelta;

            if (outcome.Moves)
            {
                Move(actor, outcome.Path);
            }

            if (outcome.PresentedArgument.HasValue)
            {
                CurrentArgument = outcome.PresentedArgument;
                IsArgumentAnswered = false;
            }
            else if (AnswersTheArgument(outcome))
            {
                IsArgumentAnswered = true;
            }

            SpendAbility(outcome);

            if (outcome.QuoteId != null)
            {
                quoteUses[outcome.QuoteId]--;
            }

            if (outcome.EndsBattle)
            {
                Status = BattleStatus.PlayerWithdrew;
            }
        }

        private static bool AnswersTheArgument(Outcome outcome) =>
            outcome.Action == ActionId.Maieutics || (outcome.Action == ActionId.Refute && outcome.Hit);

        private void Move(Debater actor, IReadOnlyList<Coord> path)
        {
            var from = actor.Position;
            var destination = path[path.Count - 1];
            var previous = path.Count > 1 ? path[path.Count - 2] : from;

            var origin = Grid[from];
            origin.OccupantId = TileData.NoOccupant;
            Grid.TrySet(from, origin);

            var target = Grid[destination];
            target.OccupantId = actor.Id;
            Grid.TrySet(destination, target);

            actor.Position = destination;
            actor.Facing = FacingRules.FromStep(previous, destination, actor.Facing);
        }

        private void SpendAbility(Outcome outcome)
        {
            if (outcome.PresentedArgument.HasValue)
            {
                IsWeakPremiseRevealed = outcome.TriggeredAbility == AbilityId.Listening;
            }

            if (outcome.TriggeredAbility is { } ability && abilityUses.TryGetValue(ability, out var left))
            {
                abilityUses[ability] = Math.Max(0, left - 1);
            }
        }

        private static void ChangeCoherence(Debater debater, int delta)
        {
            if (debater == null || delta == 0)
            {
                return;
            }

            debater.Coherence = Math.Clamp(debater.Coherence + delta, 0, debater.MaxCoherence);
        }
    }
}
