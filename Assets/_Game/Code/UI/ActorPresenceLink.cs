using System;
using System.Collections.Generic;
using System.Linq;
using Game.Content;
using Game.Core;
using UnityEngine;

namespace Game.UI
{
    [Serializable]
    public sealed class ActorPresenceLink
    {
        [SerializeField] private ActorView actor;
        [SerializeField] private List<StoryConditionEntry> conditions = new();

        public ActorPresenceLink()
        {
        }

        public ActorPresenceLink(ActorView conditionalActor, IEnumerable<StoryCondition> requires)
        {
            actor = conditionalActor;
            conditions = (requires ?? Enumerable.Empty<StoryCondition>()).Select(StoryConditionEntry.From).ToList();
        }

        public ActorView Actor => actor;

        public IReadOnlyList<StoryCondition> Conditions => conditions.Select(entry => entry.ToCondition()).ToList();

        public bool IsMet(SaveData story) => StoryCondition.AllMet(Conditions, story ?? new SaveData());
    }
}
