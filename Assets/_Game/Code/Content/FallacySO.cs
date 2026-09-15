using Game.Core;
using UnityEngine;

namespace Game.Content
{
    public sealed class FallacySO : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField, TextArea] private string playerHint;
        [SerializeField] private Sprite icon;
        [SerializeField] private FallacyCategory category;

        public string Id => id;

        public string DisplayName => displayName;

        public string Description => description;

        public string PlayerHint => playerHint;

        public Sprite Icon => icon;

        public FallacyCategory Category => category;

        public bool IsRefutable => category != FallacyCategory.Paradox;

        public void Configure(string newId, string newDisplayName, string newDescription, string newPlayerHint, FallacyCategory newCategory, Sprite newIcon)
        {
            id = newId;
            displayName = newDisplayName;
            description = newDescription;
            playerHint = newPlayerHint;
            category = newCategory;
            icon = newIcon;
        }
    }
}
