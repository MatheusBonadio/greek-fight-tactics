using System;
using System.Collections.Generic;
using System.Linq;
using Game.Content;
using Game.Core;
using UnityEngine;

namespace Game.UI
{
    [Serializable]
    public sealed class DialogueTriggerLink
    {
        [SerializeField] private Vector2Int cell;
        [SerializeField] private string dialogue;
        [SerializeField] private string cutscene;
        [SerializeField] private string flag;
        [SerializeField] private List<StoryConditionEntry> conditions = new();

        public DialogueTriggerLink()
        {
        }

        public DialogueTriggerLink(Coord triggerCell, string dialogueId, string onceFlag, IEnumerable<StoryCondition> requires = null)
        {
            cell = new Vector2Int(triggerCell.X, triggerCell.Y);
            dialogue = dialogueId;
            flag = onceFlag;
            conditions = (requires ?? Enumerable.Empty<StoryCondition>()).Select(StoryConditionEntry.From).ToList();
        }

        public static DialogueTriggerLink ForCutscene(Coord triggerCell, string cutsceneId, string onceFlag, IEnumerable<StoryCondition> requires = null) =>
            new(triggerCell, null, onceFlag, requires) { cutscene = cutsceneId };

        public Coord Cell => new(cell.x, cell.y);

        public string Dialogue => dialogue;

        public string Cutscene => cutscene;

        public bool IsCutscene => !string.IsNullOrEmpty(cutscene);

        public string Flag => flag;

        public IReadOnlyList<StoryCondition> Conditions => conditions.Select(entry => entry.ToCondition()).ToList();

        public bool IsDue(SaveData story) =>
            story != null && !StoryCondition.Has(flag).IsMet(story) && StoryCondition.AllMet(Conditions, story);
    }
}
