using Game.Core;
using UnityEngine;

namespace Game.Content
{
    public sealed class AbilitySO : ScriptableObject
    {
        [SerializeField] private AbilityId abilityId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField, Min(1)] private int usesPerBattle = 1;

        public AbilityId AbilityId => abilityId;

        public string DisplayName => displayName;

        public string Description => description;

        public int UsesPerBattle => usesPerBattle;
    }
}
