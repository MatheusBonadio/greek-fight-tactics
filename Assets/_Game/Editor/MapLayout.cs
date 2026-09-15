using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Grid;
using Newtonsoft.Json;

namespace Game.EditorTools
{
    public sealed class MapActor
    {
        public string Character { get; set; }

        public int X { get; set; }

        public int Y { get; set; }

        public Facing Facing { get; set; }
    }

    public sealed class MapLayout
    {
        public const char EmptyBlock = '-';
        public const string WaterTile = "floor_water";

        public string Id { get; set; }

        public Dictionary<char, string> FloorLegend { get; set; } = new();

        public Dictionary<char, string> BlockLegend { get; set; } = new();

        public List<string> Floor { get; set; } = new();

        public List<string> Blocks { get; set; } = new();

        public List<MapActor> Actors { get; set; } = new();

        public int Width => Floor.Count == 0 ? 0 : Floor[0].Length;

        public int Height => Floor.Count;

        public static MapLayout Load(string path)
        {
            var layout = JsonConvert.DeserializeObject<MapLayout>(File.ReadAllText(path))
                ?? throw new InvalidOperationException($"{path} is not a map layout");
            layout.Validate();
            return layout;
        }

        public string FloorAt(int x, int y) => FloorLegend[Floor[y][x]];

        public string BlockAt(int x, int y)
        {
            var symbol = Blocks[y][x];
            return symbol == EmptyBlock ? null : BlockLegend[symbol];
        }

        public bool IsWalkable(int x, int y) => FloorAt(x, y) != WaterTile && BlockAt(x, y) == null;

        public void Validate()
        {
            if (Height == 0 || Floor.Any(row => row.Length != Width))
            {
                throw new InvalidOperationException($"map {Id}: floor rows must be non-empty and share one width");
            }

            if (Blocks.Count != Height || Blocks.Any(row => row.Length != Width))
            {
                throw new InvalidOperationException($"map {Id}: block rows must match the floor size");
            }

            var unknownFloor = Floor.SelectMany(row => row).FirstOrDefault(symbol => !FloorLegend.ContainsKey(symbol));
            if (unknownFloor != default)
            {
                throw new InvalidOperationException($"map {Id}: unknown floor symbol '{unknownFloor}'");
            }

            var unknownBlock = Blocks.SelectMany(row => row).FirstOrDefault(symbol => symbol != EmptyBlock && !BlockLegend.ContainsKey(symbol));
            if (unknownBlock != default)
            {
                throw new InvalidOperationException($"map {Id}: unknown block symbol '{unknownBlock}'");
            }

            if (Actors.Count == 0)
            {
                throw new InvalidOperationException($"map {Id}: the first actor is the player, so the map needs at least one");
            }

            var occupied = new HashSet<(int, int)>();
            foreach (var actor in Actors)
            {
                if (actor.X < 0 || actor.Y < 0 || actor.X >= Width || actor.Y >= Height || !IsWalkable(actor.X, actor.Y))
                {
                    throw new InvalidOperationException($"map {Id}: {actor.Character} at ({actor.X}, {actor.Y}) is not on a walkable cell");
                }

                if (!occupied.Add((actor.X, actor.Y)))
                {
                    throw new InvalidOperationException($"map {Id}: two actors share ({actor.X}, {actor.Y})");
                }
            }
        }
    }
}
