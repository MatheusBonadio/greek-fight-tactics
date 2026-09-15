using Game.Battle;
using UnityEngine;

namespace Game.Content
{
    public sealed class QuoteItemSO : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string effectDescription;
        [SerializeField] private int coherenceDelta;
        [SerializeField] private int audienceDelta;
        [SerializeField, Min(1)] private int uses = 1;

        public string Id => id;

        public string DisplayName => displayName;

        public string EffectDescription => effectDescription;

        public int CoherenceDelta => coherenceDelta;

        public int AudienceDelta => audienceDelta;

        public int Uses => uses;

        public QuoteItem ToQuoteItem() => new(id, coherenceDelta, audienceDelta, uses);

        public void Configure(string newId, string newDisplayName, string newEffectDescription, int newCoherenceDelta, int newAudienceDelta, int newUses)
        {
            id = newId;
            displayName = newDisplayName;
            effectDescription = newEffectDescription;
            coherenceDelta = newCoherenceDelta;
            audienceDelta = newAudienceDelta;
            uses = Mathf.Max(1, newUses);
        }
    }
}
