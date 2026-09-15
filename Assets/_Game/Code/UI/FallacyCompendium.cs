using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.UI
{
    public sealed class FallacyCompendium
    {
        private readonly List<string> catalogOrder;
        private readonly HashSet<string> unlocked = new(StringComparer.Ordinal);

        public FallacyCompendium(IEnumerable<string> catalogIds)
        {
            catalogOrder = catalogIds?.Distinct().ToList() ?? throw new ArgumentNullException(nameof(catalogIds));
        }

        public int Total => catalogOrder.Count;

        public IReadOnlyList<string> UnlockedIds => catalogOrder.Where(unlocked.Contains).ToList();

        public bool IsUnlocked(string id) => id != null && unlocked.Contains(id);

        public bool Unlock(string id) => id != null && catalogOrder.Contains(id) && unlocked.Add(id);

        public static int? WeakPremiseToHighlight(Argument? argument, bool assistedMode) =>
            assistedMode && argument.HasValue && argument.Value.Premises.Count > 0 ? argument.Value.WeakPremise : (int?)null;
    }
}
