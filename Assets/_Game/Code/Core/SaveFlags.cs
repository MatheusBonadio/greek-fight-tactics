using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Game.Core
{
    public static class SaveFlags
    {
        public const string Act = "act";
        public const string DamarisBranch = "damaris_branch";
        public const string Reputation = "reputation";
        public const string Position = "position";

        private const string CustomFlagPattern = "^[a-z][a-z0-9_]{0,63}$";

        public static bool IsKnownName(string name) => name is Act or DamarisBranch or Reputation or Position;

        public static bool IsCustomName(string name) => name != null && !IsKnownName(name) && Regex.IsMatch(name, CustomFlagPattern);

        public static bool IsValidName(string name) => IsKnownName(name) || IsCustomName(name);

        public static bool TryGet(SaveData save, string name, out string value)
        {
            value = null;
            if (save == null || name == null)
            {
                return false;
            }

            switch (name)
            {
                case Act:
                    value = ((int)save.ActFlag).ToString(CultureInfo.InvariantCulture);
                    return true;
                case DamarisBranch:
                    value = save.DamarisBranch.ToString().ToLowerInvariant();
                    return true;
                case Reputation:
                    value = save.Reputation.ToString(CultureInfo.InvariantCulture);
                    return true;
                case Position:
                    value = string.Format(CultureInfo.InvariantCulture, "{0},{1}", save.Position.X, save.Position.Y);
                    return true;
            }

            return save.Flags.TryGetValue(name, out value);
        }

        public static bool TrySet(SaveData save, string name, string value, out string error)
        {
            error = null;
            if (save == null)
            {
                error = "save is required";
                return false;
            }

            switch (name)
            {
                case Act:
                    return TrySetAct(save, value, out error);
                case DamarisBranch:
                    return TrySetDamaris(save, value, out error);
                case Reputation:
                    return TrySetReputation(save, value, out error);
                case Position:
                    return TrySetPosition(save, value, out error);
            }

            if (name == null || !Regex.IsMatch(name, CustomFlagPattern))
            {
                error = $"flag name must match {CustomFlagPattern}";
                return false;
            }

            save.Flags[name] = value ?? string.Empty;
            return true;
        }

        private static bool TrySetAct(SaveData save, string value, out string error)
        {
            error = null;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) || !Enum.IsDefined(typeof(Act), number))
            {
                error = "act must be 1 or 2";
                return false;
            }

            save.ActFlag = (Act)number;
            return true;
        }

        private static bool TrySetDamaris(SaveData save, string value, out string error)
        {
            error = null;
            if (!Enum.TryParse(value, true, out DamarisChoice choice) || !Enum.IsDefined(typeof(DamarisChoice), choice))
            {
                error = "damaris_branch must be undecided, accepted or refused";
                return false;
            }

            save.DamarisBranch = choice;
            return true;
        }

        private static bool TrySetReputation(SaveData save, string value, out string error)
        {
            error = null;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var reputation))
            {
                error = "reputation must be an integer";
                return false;
            }

            save.Reputation = reputation;
            return true;
        }

        private static bool TrySetPosition(SaveData save, string value, out string error)
        {
            error = null;
            var parts = (value ?? string.Empty).Split(',');
            if (parts.Length != 2
                || !int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
                || !int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
            {
                error = "position must be \"x,y\"";
                return false;
            }

            save.Position = new Coord(x, y);
            return true;
        }
    }
}
