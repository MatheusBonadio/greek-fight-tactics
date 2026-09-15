using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Content
{
    public sealed class FallacyCatalogSO : ScriptableObject
    {
        [SerializeField] private Dictionary<string, FallacySO> fallacies = new();

        public int Count => fallacies.Count;

        public IEnumerable<FallacySO> All => fallacies.Values;

        public IEnumerable<string> RefutableIds => fallacies.Values.Where(fallacy => fallacy.IsRefutable).Select(fallacy => fallacy.Id);

        public bool TryGet(string id, out FallacySO fallacy)
        {
            fallacy = null;
            return id != null && fallacies.TryGetValue(id, out fallacy) && fallacy != null;
        }

        public void SetEntries(IEnumerable<FallacySO> entries)
        {
            fallacies = entries.ToDictionary(fallacy => fallacy.Id);
        }
    }
}
