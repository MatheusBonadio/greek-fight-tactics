using System.Collections.Generic;
using UnityEngine;

namespace Game.Content
{
    public sealed class FallbackLibrarySO : ScriptableObject
    {
        [SerializeField] private Dictionary<string, TextAsset> argumentsByEntry = new();

        public IEnumerable<string> EntryIds => argumentsByEntry.Keys;

        public int Count => argumentsByEntry.Count;

        public bool TryGetJson(string entryId, out string json)
        {
            json = null;
            if (entryId == null || !argumentsByEntry.TryGetValue(entryId, out var asset) || asset == null)
            {
                return false;
            }

            json = asset.text;
            return true;
        }

        public void SetEntries(IReadOnlyDictionary<string, TextAsset> entries)
        {
            argumentsByEntry = new Dictionary<string, TextAsset>(entries);
        }
    }
}
