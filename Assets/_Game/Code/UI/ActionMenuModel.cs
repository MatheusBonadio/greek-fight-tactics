using System;
using System.Collections.Generic;
using System.Linq;
using Game.Battle;
using Game.Core;
using Game.Grid;
using Unity.Scripting.LifecycleManagement;

namespace Game.UI
{
    public sealed class ActionMenuModel
    {
        [NoAutoStaticsCleanup]
        public static readonly IReadOnlyList<ActionId> PlayerActions = new[]
        {
            ActionId.Circulate,
            ActionId.Refute,
            ActionId.Maieutics,
            ActionId.Quote,
            ActionId.Concede,
            ActionId.Withdraw
        };

        private readonly BattleState state;
        private readonly BattleResolver resolver;
        private readonly PathFinder pathFinder = new();
        private readonly List<Coord> reachable = new();
        private readonly List<ActionMenuEntry> entries = new();

        public ActionMenuModel(BattleState state, BattleResolver resolver)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            Refresh();
        }

        public IReadOnlyList<ActionMenuEntry> Entries => entries;

        public int CursorIndex { get; private set; }

        public ActionMenuEntry Selected => entries[CursorIndex];

        public void Refresh()
        {
            entries.Clear();
            foreach (var action in PlayerActions)
            {
                entries.Add(new ActionMenuEntry(action, Probe(action)));
            }
        }

        public void MoveCursor(int delta)
        {
            var count = entries.Count;
            CursorIndex = ((CursorIndex + delta) % count + count) % count;
        }

        public bool TrySelect(ActionId action)
        {
            var index = entries.FindIndex(entry => entry.Action == action);
            if (index < 0)
            {
                return false;
            }

            CursorIndex = index;
            return true;
        }

        public bool TryConfirm(out ActionId action)
        {
            action = Selected.Action;
            return Selected.IsAvailable;
        }

        public IReadOnlyList<string> FallacyOptions(IEnumerable<string> refutableFallacyIds) =>
            Selected.Action == ActionId.Refute && Selected.IsAvailable ? refutableFallacyIds.ToList() : Array.Empty<string>();

        public IReadOnlyList<string> PremiseOptions() =>
            Selected.Action == ActionId.Maieutics && Selected.IsAvailable && state.CurrentArgument.HasValue
                ? state.CurrentArgument.Value.Premises
                : Array.Empty<string>();

        private RejectionReason Probe(ActionId action)
        {
            var player = state.Player;
            var opponent = state.Opponent;
            var play = action switch
            {
                ActionId.Circulate => ProbeCirculate(player),
                ActionId.Refute => Play.Refute(player.Id, opponent.Id, state.CurrentArgument?.FallacyId ?? string.Empty),
                ActionId.Maieutics => Play.Maieutics(player.Id, opponent.Id, 0),
                ActionId.Quote => Play.Quote(player.Id, state.QuoteIds.FirstOrDefault(id => state.TryGetQuote(id, out _, out var uses) && uses > 0)),
                ActionId.Concede => Play.Concede(player.Id),
                _ => Play.Withdraw(player.Id)
            };

            return resolver.Resolve(play, state).Rejection;
        }

        private Play ProbeCirculate(Debater player)
        {
            pathFinder.Reachable(state.Grid, player.Position, state.Rules.MoveRange, reachable);
            return Play.Circulate(player.Id, reachable.Count > 0 ? reachable[0] : player.Position);
        }
    }
}
