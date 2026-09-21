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

        public string Dialogue { get; set; }
    }

    public sealed class MapTrigger
    {
        public int X { get; set; }

        public int Y { get; set; }

        public string Dialogue { get; set; }

        public string Flag { get; set; }
    }

    public sealed class MapExit
    {
        public int X { get; set; }

        public int Y { get; set; }

        public string Location { get; set; }

        public int ArrivalX { get; set; }

        public int ArrivalY { get; set; }

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

        public List<MapExit> Exits { get; set; } = new();

        public List<MapTrigger> Triggers { get; set; } = new();

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

            var doors = new HashSet<(int, int)>();
            foreach (var exit in Exits)
            {
                if (exit.X < 0 || exit.Y < 0 || exit.X >= Width || exit.Y >= Height || !IsWalkable(exit.X, exit.Y))
                {
                    throw new InvalidOperationException($"map {Id}: the exit at ({exit.X}, {exit.Y}) is not on a walkable cell");
                }

                if (string.IsNullOrWhiteSpace(exit.Location))
                {
                    throw new InvalidOperationException($"map {Id}: the exit at ({exit.X}, {exit.Y}) has no destination");
                }

                if (!doors.Add((exit.X, exit.Y)))
                {
                    throw new InvalidOperationException($"map {Id}: two exits share ({exit.X}, {exit.Y})");
                }
            }

            if (!string.IsNullOrEmpty(Actors[0].Dialogue))
            {
                throw new InvalidOperationException($"map {Id}: the player {Actors[0].Character} cannot hold a dialogue");
            }

            var triggerCells = new HashSet<(int, int)>();
            foreach (var trigger in Triggers)
            {
                var cell = (trigger.X, trigger.Y);
                if (trigger.X < 0 || trigger.Y < 0 || trigger.X >= Width || trigger.Y >= Height || !IsWalkable(trigger.X, trigger.Y))
                {
                    throw new InvalidOperationException($"map {Id}: the trigger at ({trigger.X}, {trigger.Y}) is not on a walkable cell");
                }

                if (string.IsNullOrWhiteSpace(trigger.Dialogue))
                {
                    throw new InvalidOperationException($"map {Id}: the trigger at ({trigger.X}, {trigger.Y}) has no dialogue");
                }

                if (!SaveFlags.IsCustomName(trigger.Flag))
                {
                    throw new InvalidOperationException($"map {Id}: the trigger at ({trigger.X}, {trigger.Y}) needs a story flag in snake case, found '{trigger.Flag}'");
                }

                if (!triggerCells.Add(cell) || doors.Contains(cell) || occupied.Contains(cell))
                {
                    throw new InvalidOperationException($"map {Id}: the trigger at ({trigger.X}, {trigger.Y}) shares its cell with another trigger, an exit or an actor");
                }
            }
        }

        public IEnumerable<string> DialogueIds =>
            Actors.Select(actor => actor.Dialogue).Concat(Triggers.Select(trigger => trigger.Dialogue)).Where(id => !string.IsNullOrEmpty(id)).Distinct();
    }
}
