using System.Collections.Generic;

namespace Game.Core
{
    public static class ActionUnlocks
    {
        public const string MaieuticsFlag = "maieutics_unlocked";

        public static string FlagFor(ActionId action) => action == ActionId.Maieutics ? MaieuticsFlag : null;

        public static bool IsUnlocked(ActionId action, SaveData save)
        {
            var flag = FlagFor(action);
            return flag == null || (save != null && StoryCondition.Has(flag).IsMet(save));
        }

        public static List<ActionId> LockedIn(SaveData save, IEnumerable<ActionId> actions)
        {
            var locked = new List<ActionId>();
            foreach (var action in actions)
            {
                if (!IsUnlocked(action, save))
                {
                    locked.Add(action);
                }
            }

            return locked;
        }
    }
}
