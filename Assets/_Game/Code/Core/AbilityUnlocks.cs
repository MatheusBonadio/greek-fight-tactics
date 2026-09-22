using System;
using System.Collections.Generic;

namespace Game.Core
{
    public static class AbilityUnlocks
    {
        public static bool IsOwned(AbilityId ability, SaveData save) =>
            save != null && StoryCondition.Has(DamarisOffer.AbilityFlag(ability)).IsMet(save);

        public static List<AbilityId> OwnedIn(SaveData save)
        {
            var owned = new List<AbilityId>();
            foreach (AbilityId ability in Enum.GetValues(typeof(AbilityId)))
            {
                if (IsOwned(ability, save))
                {
                    owned.Add(ability);
                }
            }

            return owned;
        }
    }
}
