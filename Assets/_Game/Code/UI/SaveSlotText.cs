using System;
using Game.Content;
using Game.Core;
using Game.Net;

namespace Game.UI
{
    public static class SaveSlotText
    {
        public const string TitleSaveKey = "slots.title_save";
        public const string TitleLoadKey = "slots.title_load";
        public const string SlotKey = "slots.slot";
        public const string EmptyLineKey = "slots.empty_line";
        public const string CorruptLineKey = "slots.corrupt_line";
        public const string SummaryLineKey = "slots.summary_line";
        public const string PlaytimeKey = "slots.playtime";
        public const string ConfirmOverwriteKey = "slots.confirm_overwrite";
        public const string YesKey = "slots.yes";
        public const string NoKey = "slots.no";
        public const string UnknownLocationId = "unknown";

        private const string ActPrefix = "act.";
        private const string LocationPrefix = "location.";
        private const long SecondsPerMinute = 60;
        private const long MinutesPerHour = 60;

        public static string ActKey(Act act) => ActPrefix + UiTextKeys.SnakeCase(act.ToString());

        public static string LocationKey(string locationId) =>
            LocationPrefix + (string.IsNullOrWhiteSpace(locationId) ? UnknownLocationId : locationId);

        public static string Playtime(long seconds, UiTextSO text)
        {
            var totalMinutes = Math.Max(0, seconds) / SecondsPerMinute;
            return string.Format(text.Get(PlaytimeKey), totalMinutes / MinutesPerHour, totalMinutes % MinutesPerHour);
        }

        public static string Line(SaveSlotSummary slot, UiTextSO text)
        {
            var label = string.Format(text.Get(SlotKey), slot.Index + 1);
            if (!slot.Exists)
            {
                return string.Format(text.Get(EmptyLineKey), label);
            }

            if (slot.IsCorrupt)
            {
                return string.Format(text.Get(CorruptLineKey), label);
            }

            return string.Format(text.Get(SummaryLineKey), label, text.Get(ActKey(slot.Act)), text.Get(LocationKey(slot.Location)), Playtime(slot.PlaytimeSeconds, text));
        }
    }
}
