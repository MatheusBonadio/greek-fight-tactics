using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

        public string Cutscene { get; set; }

        public string Flag { get; set; }

        public List<MapCondition> Conditions { get; set; } = new();

        public bool IsCutscene => !string.IsNullOrWhiteSpace(Cutscene);

        public IEnumerable<StoryCondition> StoryConditions => Conditions.Select(condition => condition.ToCondition());
    }

    public sealed class MapCutsceneStep
    {
        public double At { get; set; }

        public CutsceneAction Action { get; set; }

        public string Actor { get; set; }

        public string Toward { get; set; }

        public int? X { get; set; }

        public int? Y { get; set; }

        public string Dialogue { get; set; }

        public bool HasCell => X.HasValue && Y.HasValue;

        public Coord? Cell => HasCell ? new Coord(X.Value, Y.Value) : null;
    }

    public sealed class MapCutscene
    {
        public string Id { get; set; }

        public List<MapCutsceneStep> Steps { get; set; } = new();

        public double LastStepAt => Steps.Count == 0 ? 0d : Steps[^1].At;

        public IEnumerable<string> ActorIds =>
            Steps.SelectMany(step => new[] { step.Actor, step.Toward }).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct();
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

        public Facing? Heading { get; set; }

        public string Label { get; set; }
    }

    public sealed class MapLayout
    {
        public const char EmptyBlock = '-';
        public const string WaterTile = "floor_water";
        public const string CutsceneIdPattern = "^[a-z][a-z0-9_]*$";
        public const char GroundLevelSymbol = '0';

        public string Id { get; set; }

        public Dictionary<char, string> FloorLegend { get; set; } = new();

        public Dictionary<char, string> BlockLegend { get; set; } = new();

        public List<string> Floor { get; set; } = new();

        public List<string> Blocks { get; set; } = new();

        public List<string> Heights { get; set; } = new();

        public List<MapActor> Actors { get; set; } = new();

        public List<MapExit> Exits { get; set; } = new();

        public List<MapTrigger> Triggers { get; set; } = new();

        public List<MapCue> Cues { get; set; } = new();

        public List<MapCutscene> Cutscenes { get; set; } = new();

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

        public int LevelAt(int x, int y) => Heights.Count == 0 ? TileData.GroundLevel : Heights[y][x] - GroundLevelSymbol;

        public int MaxLevel => Enumerable.Range(0, Height).SelectMany(y => Enumerable.Range(0, Width).Select(x => LevelAt(x, y))).DefaultIfEmpty(0).Max();

        public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public bool CanStep((int X, int Y) from, (int X, int Y) to) =>
            Contains(from.X, from.Y)
            && Contains(to.X, to.Y)
            && Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y) == 1
            && IsWalkable(from.X, from.Y)
            && IsWalkable(to.X, to.Y)
            && Math.Abs(LevelAt(from.X, from.Y) - LevelAt(to.X, to.Y)) <= TerrainSteps.MaxClimb;

        public Facing HeadingOf(MapExit exit)
        {
            if (exit.Heading.HasValue)
            {
                return exit.Heading.Value;
            }

            if (exit.X == 0)
            {
                return Facing.SouthWest;
            }

            if (exit.X == Width - 1)
            {
                return Facing.NorthEast;
            }

            return exit.Y == Height - 1 ? Facing.NorthWest : Facing.SouthEast;
        }

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

            ValidateHeights();

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

                if (exit.Heading.HasValue && !Enum.IsDefined(typeof(Facing), exit.Heading.Value))
                {
                    throw new InvalidOperationException($"map {Id}: the exit at ({exit.X}, {exit.Y}) points to the unknown heading {exit.Heading}");
                }

                if (exit.Label != null && string.IsNullOrWhiteSpace(exit.Label))
                {
                    throw new InvalidOperationException($"map {Id}: the exit at ({exit.X}, {exit.Y}) has a blank label");
                }
            }

            if (!string.IsNullOrEmpty(Actors[0].Dialogue))
            {
                throw new InvalidOperationException($"map {Id}: the player {Actors[0].Character} cannot hold a dialogue");
            }

            ValidateCutscenes();

            var triggerCells = new HashSet<(int, int)>();
            foreach (var trigger in Triggers)
            {
                var cell = (trigger.X, trigger.Y);
                if (trigger.X < 0 || trigger.Y < 0 || trigger.X >= Width || trigger.Y >= Height || !IsWalkable(trigger.X, trigger.Y))
                {
                    throw new InvalidOperationException($"map {Id}: the trigger at ({trigger.X}, {trigger.Y}) is not on a walkable cell");
                }

                if (string.IsNullOrWhiteSpace(trigger.Dialogue) == !trigger.IsCutscene)
                {
                    throw new InvalidOperationException($"map {Id}: the trigger at ({trigger.X}, {trigger.Y}) needs either a dialogue or a cutscene");
                }

                if (trigger.IsCutscene && Cutscenes.All(cutscene => cutscene.Id != trigger.Cutscene))
                {
                    throw new InvalidOperationException($"map {Id}: the trigger at ({trigger.X}, {trigger.Y}) plays the unknown cutscene {trigger.Cutscene}");
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

        private void ValidateHeights()
        {
            if (Heights.Count == 0)
            {
                return;
            }

            if (Heights.Count != Height || Heights.Any(row => row.Length != Width))
            {
                throw new InvalidOperationException($"map {Id}: height rows must match the floor size");
            }

            var invalid = Heights.SelectMany(row => row).FirstOrDefault(symbol => symbol < GroundLevelSymbol || symbol > GroundLevelSymbol + TerrainSorting.MaxLevel);
            if (invalid != default)
            {
                throw new InvalidOperationException($"map {Id}: the height '{invalid}' is not a level from 0 to {TerrainSorting.MaxLevel}");
            }
        }

        private void ValidateCutscenes()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var cutscene in Cutscenes)
            {
                if (cutscene.Id == null || !Regex.IsMatch(cutscene.Id, CutsceneIdPattern) || !ids.Add(cutscene.Id))
                {
                    throw new InvalidOperationException($"map {Id}: every cutscene needs its own id in snake case, found '{cutscene.Id}'");
                }

                if (cutscene.Steps.Count == 0)
                {
                    throw new InvalidOperationException($"map {Id}: the cutscene {cutscene.Id} has no steps");
                }

                var previous = 0d;
                foreach (var step in cutscene.Steps)
                {
                    var owner = $"the cutscene {cutscene.Id} at {step.At}s";
                    if (step.At <= previous)
                    {
                        throw new InvalidOperationException($"map {Id}: {owner} must come after the previous step and after 0s");
                    }

                    previous = step.At;
                    ValidateStep(owner, step);
                }
            }
        }

        private void ValidateStep(string owner, MapCutsceneStep step)
        {
            switch (step.Action)
            {
                case CutsceneAction.Dialogue:
                    if (string.IsNullOrWhiteSpace(step.Dialogue))
                    {
                        throw new InvalidOperationException($"map {Id}: {owner} has no dialogue");
                    }

                    return;
                case CutsceneAction.Face:
                    RequireCastMember(owner, step.Actor);
                    if (string.IsNullOrWhiteSpace(step.Toward) == !step.HasCell)
                    {
                        throw new InvalidOperationException($"map {Id}: {owner} faces either an actor or a cell");
                    }

                    if (step.HasCell)
                    {
                        RequireInside(owner, step.X.Value, step.Y.Value);
                    }
                    else
                    {
                        RequireCastMember(owner, step.Toward);
                    }

                    return;
                case CutsceneAction.Walk:
                    RequireCastMember(owner, step.Actor);
                    if (!step.HasCell)
                    {
                        throw new InvalidOperationException($"map {Id}: {owner} walks without a destination");
                    }

                    RequireInside(owner, step.X.Value, step.Y.Value);
                    if (!IsWalkable(step.X.Value, step.Y.Value))
                    {
                        throw new InvalidOperationException($"map {Id}: {owner} walks to the blocked cell ({step.X}, {step.Y})");
                    }

                    return;
                case CutsceneAction.Leave:
                    RequireCastMember(owner, step.Actor);
                    if (step.Actor == Actors[0].Character)
                    {
                        throw new InvalidOperationException($"map {Id}: {owner} cannot send the player off the map");
                    }

                    return;
                default:
                    throw new InvalidOperationException($"map {Id}: {owner} has the unknown action {step.Action}");
            }
        }

        private void RequireCastMember(string owner, string character)
        {
            if (string.IsNullOrWhiteSpace(character) || Actors.Count(actor => actor.Character == character) != 1)
            {
                throw new InvalidOperationException($"map {Id}: {owner} needs an actor that appears exactly once on the map, found '{character}'");
            }
        }

        private void RequireInside(string owner, int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
            {
                throw new InvalidOperationException($"map {Id}: {owner} points outside the map at ({x}, {y})");
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
                .Concat(Cutscenes.SelectMany(cutscene => cutscene.Steps).Where(step => step.Action == CutsceneAction.Dialogue).Select(step => step.Dialogue))
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct();
    }
}
