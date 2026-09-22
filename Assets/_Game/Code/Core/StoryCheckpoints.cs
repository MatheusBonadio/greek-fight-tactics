using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public sealed class StoryCheckpoint
    {
        public StoryCheckpoint(string id, string label, string location, params StoryEffect[] effects)
        {
            Id = id;
            Label = label;
            Location = location;
            Effects = effects ?? new StoryEffect[0];
        }

        public string Id { get; }

        public string Label { get; }

        public string Location { get; }

        public IReadOnlyList<StoryEffect> Effects { get; }

        public bool IsReached(SaveData save) =>
            save != null && Effects.All(effect => Matches(save, effect));

        public bool Apply(SaveData save)
        {
            var applied = false;
            foreach (var effect in Effects)
            {
                applied |= effect.TryApply(save, out _);
            }

            return applied;
        }

        private static bool Matches(SaveData save, StoryEffect effect) =>
            SaveFlags.TryGet(save, effect.Flag, out var value) && value == effect.Value;
    }

    public static class StoryCheckpoints
    {
        public const string Workshop = "kerameikos";
        public const string Agora = "agora";
        public const string Archive = "archive";
        public const int NotReached = -1;

        private static readonly string[] NeighbourhoodDuellists = { "oron", "phaidra", "kritolaos" };

        public static IReadOnlyList<StoryCheckpoint> All { get; } = new[]
        {
            new StoryCheckpoint("errand", "I.1 the errand", Workshop,
                Flag("tutorial_first_steps"), Flag("tutorial_fog"), Flag("tutorial_talk"), Flag("aristeu_errand"), Flag("left_workshop")),
            new StoryCheckpoint("thrasybios", "I.3 the market duel", Agora,
                Flag("saw_thrasybios_demo"), Flag("thrasybios_tutorial_done"), Won("thrasybios"), Flag("headed_home")),
            new StoryCheckpoint("maieutics", "I.4 the door lesson", Workshop,
                Flag("met_menexeno"), Flag("menexeno_condition"), Flag("menexeno_avoids_past"), Flag("menexeno_door_scene"), Flag(ActionUnlocks.MaieuticsFlag)),
            new StoryCheckpoint("pressure", "I.6 the workshop empties", Workshop,
                Won(NeighbourhoodDuellists[0]), Won(NeighbourhoodDuellists[1]), Won(NeighbourhoodDuellists[2]),
                Flag("heard_asebeia"), Flag("saw_rumor_spread"), Flag("saw_empty_workshop"), Flag("saw_wheel_sold"), Flag("worried_for_home")),
            new StoryCheckpoint("damaris", "I.7 the offer answered", Workshop,
                Flag("damaris_offer_scene"), new StoryEffect(SaveFlags.DamarisBranch, DamarisChoice.Refused.ToString().ToLowerInvariant())),
            new StoryCheckpoint("trial", "I.8 the herald calls", Agora,
                Flag("street_trial_called")),
            new StoryCheckpoint("searching", "I.9 the archive door", Agora,
                Won("thrasybios_street_trial"), Flag("searching_menexeno")),
            new StoryCheckpoint("act_two", "I.10 the bronze seal", Archive,
                Flag("entered_archive"), Flag("asked_archivist"), Flag("read_juror_list"), Flag("knows_menexeno_vote"), Flag("bronze_seal"),
                new StoryEffect(SaveFlags.Act, ((int)Act.Two).ToString()))
        };

        public static int IndexOf(SaveData save)
        {
            var reached = NotReached;
            for (var index = 0; index < All.Count; index++)
            {
                if (!All[index].IsReached(save))
                {
                    break;
                }

                reached = index;
            }

            return reached;
        }

        public static StoryCheckpoint Reached(SaveData save)
        {
            var index = IndexOf(save);
            return index == NotReached ? null : All[index];
        }

        public static StoryCheckpoint Next(SaveData save)
        {
            var index = IndexOf(save) + 1;
            return index < All.Count ? All[index] : null;
        }

        private static StoryEffect Flag(string name) => new(name);

        private static StoryEffect Won(string opponentId) => new(DuelStoryFlags.FlagFor(opponentId), DuelStoryFlags.Won);
    }
}
