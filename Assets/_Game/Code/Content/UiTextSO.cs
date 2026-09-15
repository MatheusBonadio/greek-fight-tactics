using System.Collections.Generic;
using UnityEngine;

namespace Game.Content
{
    public sealed class UiTextSO : ScriptableObject
    {
        public const string MissingPrefix = "#";

        [SerializeField] private Dictionary<string, string> entries = new();

        public int Count => entries.Count;

        public IEnumerable<string> Keys => entries.Keys;

        public bool Contains(string key) => key != null && entries.ContainsKey(key);

        public string Get(string key) =>
            key != null && entries.TryGetValue(key, out var text) && !string.IsNullOrEmpty(text) ? text : MissingPrefix + key;

        public void SetEntries(IReadOnlyDictionary<string, string> newEntries)
        {
            entries = new Dictionary<string, string>(newEntries);
        }
    }
}
