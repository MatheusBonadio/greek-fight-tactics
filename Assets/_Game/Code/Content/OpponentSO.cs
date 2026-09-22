using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Content
{
    public sealed class OpponentSO : ScriptableObject
    {
        public const int MaxBarValue = 100;

        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private Texture2D sheet;
        [SerializeField, Range(1, MaxBarValue)] private int maxCoherence = 50;
        [SerializeField, Range(0, MaxBarValue)] private int initialAudience = 50;
        [SerializeField] private List<FallacySO> fallacyPool = new();
        [SerializeField] private Act act = Act.One;
        [SerializeField] private Profile profile = Profile.Sophist;
        [SerializeField] private OpponentTutorial tutorial = new();
        [SerializeField] private string sheetId;
        [SerializeField] private OpponentPhases phases = new();

        public string Id => id;

        public string DisplayName => displayName;

        public Texture2D Sheet => sheet;

        public string SheetId => string.IsNullOrEmpty(sheetId) ? id : sheetId;

        public OpponentPhases Phases => phases ?? new OpponentPhases();

        public bool IsPhased => Phases.IsDefined;

        public int MaxCoherence => maxCoherence;

        public int InitialAudience => initialAudience;

        public IReadOnlyList<FallacySO> FallacyPool => fallacyPool;

        public Act Act => act;

        public Profile Profile => profile;

        public OpponentTutorial Tutorial => tutorial;

        public void Configure(string newId, string newDisplayName, int newMaxCoherence, int newInitialAudience, IEnumerable<FallacySO> newFallacyPool, Act newAct, Profile newProfile)
        {
            id = newId;
            displayName = newDisplayName;
            maxCoherence = Mathf.Clamp(newMaxCoherence, 1, MaxBarValue);
            initialAudience = Mathf.Clamp(newInitialAudience, 0, MaxBarValue);
            fallacyPool = new List<FallacySO>(newFallacyPool);
            act = newAct;
            profile = newProfile;
        }

        public void ConfigureSheet(Texture2D newSheet)
        {
            sheet = newSheet;
        }

        public void ConfigureSheet(Texture2D newSheet, string newSheetId)
        {
            sheet = newSheet;
            sheetId = newSheetId;
        }

        public void ConfigurePhases(OpponentPhases newPhases)
        {
            phases = newPhases ?? new OpponentPhases();
        }

        public void ConfigureTutorial(OpponentTutorial newTutorial)
        {
            tutorial = newTutorial ?? new OpponentTutorial();
        }
    }
}
