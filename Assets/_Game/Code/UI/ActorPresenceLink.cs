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
        [SerializeField] private bool walksIn;
        [SerializeField] private Vector2Int entrance;

        public ActorPresenceLink()
        {
        }

        public ActorPresenceLink(ActorView conditionalActor, IEnumerable<StoryCondition> requires, Coord? walksInFrom = null)
        {
            actor = conditionalActor;
            conditions = (requires ?? Enumerable.Empty<StoryCondition>()).Select(StoryConditionEntry.From).ToList();
            walksIn = walksInFrom.HasValue;
            entrance = walksInFrom.HasValue ? new Vector2Int(walksInFrom.Value.X, walksInFrom.Value.Y) : Vector2Int.zero;
        }

        public ActorView Actor => actor;

        public bool WalksIn => walksIn;

        public Coord Entrance => new(entrance.x, entrance.y);

        public IReadOnlyList<StoryCondition> Conditions => conditions.Select(entry => entry.ToCondition()).ToList();

        public bool IsMet(SaveData story) => StoryCondition.AllMet(Conditions, story ?? new SaveData());
    }
}
