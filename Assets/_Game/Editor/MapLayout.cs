using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Grid;
using Newtonsoft.Json;

namespace Game.EditorTools
{
    public sealed class MapCondition
    {
        public string Flag { get; set; }

        [JsonProperty("equals")]
        public string EqualsValue { get; set; }

        public bool Absent { get; set; }

        public StoryCondition ToCondition() => new(Flag, EqualsValue, Absent);

        public static MapCondition Has(string flag) => new() { Flag = flag };

        public static MapCondition Lacks(string flag) => new() { Flag = flag, Absent = true };
    }

    public sealed class MapActor
    {
        public string Character { get; set; }

        public int X { get; set; }

        public int Y { get; set; }

        public Facing Facing { get; set; }

        public string Dialogue { get; set; }

        public List<MapCondition> Conditions { get; set; } = new();

        public bool IsConditional => Conditions.Count > 0;

        public IEnumerable<StoryCondition> StoryConditions => Conditions.Select(condition => condition.ToCondition());
    }

    public sealed class MapTrigger
    {
        public int X { get; set; }

        public int Y { get; set; }

        public string Dialogue { get; set; }

        public string Flag { get; set; }

        public List<MapCondition> Conditions { get; set; } = new();

        public IEnumerable<StoryCondition> StoryConditions => Conditions.Select(condition => condition.ToCondition());
    }

    public sealed class MapCue
    {
        public ExplorationCue On { get; set; }

        public string Dialogue { get; set; }

        public string Flag { get; set; }

        public List<MapCondition> Conditions { get; set; } = new();

        public IEnumerable<StoryCondition> StoryConditions => Conditions.Select(condition => condition.ToCondition());
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

        public List<MapCue> Cues { get; set; } = new();

        public int? FogRevealDepth { get; set; }

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

                ValidateConditions($"{actor.Character} at ({actor.X}, {actor.Y})", actor.Conditions);
            }

            if (Actors[0].IsConditional)
            {
                throw new InvalidOperationException($"map {Id}: the player {Actors[0].Character} is always on the map and cannot wait for story flags");
            }

            if (FogRevealDepth is < 0)
            {
                throw new InvalidOperationException($"map {Id}: fogRevealDepth cannot be negative");
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

                ValidateConditions($"the trigger at ({trigger.X}, {trigger.Y})", trigger.Conditions);
            }

            foreach (var cue in Cues)
            {
                if (!Enum.IsDefined(typeof(ExplorationCue), cue.On))
                {
                    throw new InvalidOperationException($"map {Id}: the cue {cue.Dialogue} listens to an unknown event {cue.On}");
                }

                if (string.IsNullOrWhiteSpace(cue.Dialogue))
                {
                    throw new InvalidOperationException($"map {Id}: a {cue.On} cue has no dialogue");
                }

                if (!SaveFlags.IsCustomName(cue.Flag))
                {
                    throw new InvalidOperationException($"map {Id}: the cue {cue.Dialogue} needs a story flag in snake case, found '{cue.Flag}'");
                }

                ValidateConditions($"the cue {cue.Dialogue}", cue.Conditions);
            }
        }

        private void ValidateConditions(string owner, IEnumerable<MapCondition> conditions)
        {
            foreach (var condition in (conditions ?? Enumerable.Empty<MapCondition>()).Select(entry => entry.ToCondition()))
            {
                if (!condition.IsValid)
                {
                    throw new InvalidOperationException($"map {Id}: {owner} has the invalid condition '{condition}'");
                }
            }
        }

        public IEnumerable<string> DialogueIds =>
            Actors.Select(actor => actor.Dialogue)
                .Concat(Triggers.Select(trigger => trigger.Dialogue))
                .Concat(Cues.Select(cue => cue.Dialogue))
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct();
    }
}
