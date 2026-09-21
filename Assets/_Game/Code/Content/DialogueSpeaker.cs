using System;
using UnityEngine;

namespace Game.Content
{
    [Serializable]
    public sealed class DialogueSpeaker
    {
        [SerializeField] private string displayName;
        [SerializeField] private string portrait;

        public DialogueSpeaker()
        {
        }

        public DialogueSpeaker(string newDisplayName, string newPortrait)
        {
            displayName = newDisplayName;
            portrait = newPortrait;
        }

        public string DisplayName => displayName;

        public string Portrait => portrait;

        public bool HasPortrait => !string.IsNullOrEmpty(portrait);
    }
}
