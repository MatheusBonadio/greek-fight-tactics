using System;
using System.Collections.Generic;
using System.Linq;
using Game.Content;
using Game.Core;
using UnityEngine;

namespace Game.UI
{
    [Serializable]
    public sealed class BlockPresenceLink
    {
        [SerializeField] private Vector2Int cell;
        [SerializeField] private byte openCost;
        [SerializeField] private List<StoryConditionEntry> conditions = new();

        public BlockPresenceLink()
        {
        }

        public BlockPresenceLink(Coord blockCell, byte costWithoutBlock, IEnumerable<StoryCondition> requires)
        {
            cell = new Vector2Int(blockCell.X, blockCell.Y);
            openCost = costWithoutBlock;
            conditions = (requires ?? Enumerable.Empty<StoryCondition>()).Select(StoryConditionEntry.From).ToList();
        }

        public Coord Cell => new(cell.x, cell.y);

        public byte OpenCost => openCost;

        public IReadOnlyList<StoryCondition> Conditions => conditions.Select(entry => entry.ToCondition()).ToList();

        public bool IsMet(SaveData story) => StoryCondition.AllMet(Conditions, story ?? new SaveData());
    }
}
