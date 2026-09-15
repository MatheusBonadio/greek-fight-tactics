using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Content
{
    public sealed class OpponentSO : ScriptableObject
    {
        public const int MaxBarValue = 100;

        [SerializeField] private string displayName;
        [SerializeField] private Texture2D sheet;
        [SerializeField, Range(1, MaxBarValue)] private int maxCoherence = 50;
        [SerializeField, Range(0, MaxBarValue)] private int initialAudience = 50;
        [SerializeField] private List<FallacySO> fallacyPool = new();
        [SerializeField] private Act act = Act.One;
        [SerializeField] private Profile profile = Profile.Sophist;

        public string DisplayName => displayName;

        public Texture2D Sheet => sheet;

        public int MaxCoherence => maxCoherence;

        public int InitialAudience => initialAudience;

        public IReadOnlyList<FallacySO> FallacyPool => fallacyPool;

        public Act Act => act;

        public Profile Profile => profile;
    }
}
