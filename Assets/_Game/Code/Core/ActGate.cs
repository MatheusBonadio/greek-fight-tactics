using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public static class ActGate
    {
        public const Act FirstAct = Act.One;

        public static Act ActOf(SaveData save) => save?.ActFlag ?? FirstAct;

        public static bool IsReached(SaveData save, Act act) => ActOf(save) >= act;

        public static bool IsOpen(SaveData save, Act requiredAct, IEnumerable<StoryCondition> conditions) =>
            IsReached(save, requiredAct) && StoryCondition.AllMet(conditions, save ?? new SaveData());

        public static bool IsGated(Act requiredAct, IEnumerable<StoryCondition> conditions) =>
            requiredAct > FirstAct || (conditions != null && conditions.Any());
    }
}
