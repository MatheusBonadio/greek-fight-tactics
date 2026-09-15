using System;

namespace Game.Core
{
    public static class DamarisOffer
    {
        public const int AcceptedDrachmas = 200;
        public const int MenexenoAbsentDays = 3;
        public const string DrachmasItem = "drachmas";
        public const string AbilityFlagPrefix = "ability_";
        public const string MenexenoAbsentDaysFlag = "menexeno_absent_days";
        public const string MenexenoRespectFlag = "menexeno_respect";
        public const string EnabledValue = "true";

        public static string AbilityFlag(AbilityId ability) => AbilityFlagPrefix + ability.ToString().ToLowerInvariant();

        public static AbilityId? GrantedAbility(DamarisChoice choice) => choice switch
        {
            DamarisChoice.Accepted => AbilityId.Inversion,
            DamarisChoice.Refused => AbilityId.Listening,
            _ => null
        };

        public static void Apply(SaveData save, DamarisChoice choice)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            if (choice == DamarisChoice.Undecided)
            {
                throw new ArgumentException("Damaris's offer must be accepted or refused.", nameof(choice));
            }

            if (save.DamarisBranch != DamarisChoice.Undecided)
            {
                throw new InvalidOperationException($"Damaris's offer was already {save.DamarisBranch}.");
            }

            save.DamarisBranch = choice;
            save.Flags[AbilityFlag(GrantedAbility(choice).Value)] = EnabledValue;

            if (choice == DamarisChoice.Accepted)
            {
                save.Inventory[DrachmasItem] = (save.Inventory.TryGetValue(DrachmasItem, out var drachmas) ? drachmas : 0) + AcceptedDrachmas;
                save.Flags[MenexenoAbsentDaysFlag] = MenexenoAbsentDays.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return;
            }

            save.Flags[MenexenoRespectFlag] = EnabledValue;
        }
    }
}
