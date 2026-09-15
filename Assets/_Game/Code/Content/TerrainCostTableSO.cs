using System.Collections.Generic;
using Game.Core;
using Game.Grid;
using UnityEngine;

namespace Game.Content
{
    public sealed class TerrainCostTableSO : ScriptableObject
    {
        [SerializeField] private Dictionary<TerrainId, byte> costs = new();

        public byte CostOf(TerrainId terrain) => costs.TryGetValue(terrain, out var cost) ? cost : TileData.ImpassableCost;

        public void SetCosts(IReadOnlyDictionary<TerrainId, byte> newCosts)
        {
            costs = new Dictionary<TerrainId, byte>(newCosts);
        }
    }
}
