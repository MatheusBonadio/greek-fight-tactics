using System;
using System.Collections.Generic;
using System.Linq;
using Game.Content;
using Game.Core;
using UnityEngine;

namespace Game.UI
{
    [Serializable]
    public sealed class StoryCueLink
    {
        [SerializeField] private ExplorationCue on;
        [SerializeField] private string dialogue;
        [SerializeField] private string flag;
        [SerializeField] private List<StoryConditionEntry> conditions = new();

        public StoryCueLink()
        {
        }

        public StoryCueLink(ExplorationCue cue, string dialogueId, string onceFlag, IEnumerable<StoryCondition> requires = null)
        {
            on = cue;
            dialogue = dialogueId;
            flag = onceFlag;
            conditions = (requires ?? Enumerable.Empty<StoryCondition>()).Select(StoryConditionEntry.From).ToList();
        }

        public ExplorationCue On => on;

        public string Dialogue => dialogue;

        public string Flag => flag;

        public IReadOnlyList<StoryCondition> Conditions => conditions.Select(entry => entry.ToCondition()).ToList();

        public bool IsDue(ExplorationCue cue, SaveData story) =>
            cue == on && story != null && !StoryCondition.Has(flag).IsMet(story) && StoryCondition.AllMet(Conditions, story);
    }
}
