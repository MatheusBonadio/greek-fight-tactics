using UnityEngine;

namespace Game.Content
{
    public sealed class QuoteItemSO : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string effectDescription;
        [SerializeField] private int coherenceDelta;
        [SerializeField] private int audienceDelta;
        [SerializeField, Min(1)] private int uses = 1;

        public string DisplayName => displayName;

        public string EffectDescription => effectDescription;

        public int CoherenceDelta => coherenceDelta;

        public int AudienceDelta => audienceDelta;

        public int Uses => uses;
    }
}
