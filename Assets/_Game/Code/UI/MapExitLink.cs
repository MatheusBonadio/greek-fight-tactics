using System;
using Game.Core;
using Game.Grid;
using UnityEngine;

namespace Game.UI
{
    [Serializable]
    public sealed class MapExitLink
    {
        [SerializeField] private Vector2Int cell;
        [SerializeField] private string location;
        [SerializeField] private Vector2Int arrival;
        [SerializeField] private Facing facing;

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

        public Coord Cell => new(cell.x, cell.y);

        public string Location => location;

        public Coord Arrival => new(arrival.x, arrival.y);

        public Facing Facing => facing;
    }
}
