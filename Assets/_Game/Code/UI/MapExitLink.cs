using System;
using System.Collections.Generic;
using System.Linq;
using Game.Content;
using Game.Core;
using Game.Grid;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Game.UI
{
    [Serializable]
    public sealed class MapExitLink
    {
        [SerializeField] private Vector2Int cell;
        [SerializeField] private string location;
        [SerializeField] private Vector2Int arrival;
        [SerializeField] private Facing facing;
        [SerializeField] private Facing heading;
        [SerializeField] private string label;
        [SerializeField] private TileBase marker;
        [SerializeField] private Act requiredAct = ActGate.FirstAct;
        [SerializeField] private List<StoryConditionEntry> conditions = new();

        public MapExitLink()
        {
        }

        public MapExitLink(Coord exitCell, string targetLocation, Coord arrivalCell, Facing arrivalFacing)
        {
            cell = new Vector2Int(exitCell.X, exitCell.Y);
            location = targetLocation;
            arrival = new Vector2Int(arrivalCell.X, arrivalCell.Y);
            facing = arrivalFacing;
        }

        public MapExitLink(Coord exitCell, string targetLocation, Coord arrivalCell, Facing arrivalFacing, Facing exitHeading, string exitLabel, TileBase exitMarker)
            : this(exitCell, targetLocation, arrivalCell, arrivalFacing)
        {
            heading = exitHeading;
            label = exitLabel;
            marker = exitMarker;
        }

        public Coord Cell => new(cell.x, cell.y);

        public string Location => location;

        public Coord Arrival => new(arrival.x, arrival.y);

        public Facing Facing => facing;

        public Facing Heading => heading;

        public string Label => label;

        public TileBase Marker => marker;

        public Act RequiredAct => requiredAct;

        public IReadOnlyList<StoryCondition> Conditions => (conditions ?? new List<StoryConditionEntry>()).Select(entry => entry.ToCondition()).ToList();

        public bool IsGated => ActGate.IsGated(requiredAct, Conditions);

        public bool IsOpenFor(SaveData story) => ActGate.IsOpen(story, requiredAct, Conditions);

        public MapExitLink Gated(Act act, IEnumerable<StoryCondition> requires)
        {
            requiredAct = act;
            conditions = (requires ?? Enumerable.Empty<StoryCondition>()).Select(StoryConditionEntry.From).ToList();
            return this;
        }
    }
}
