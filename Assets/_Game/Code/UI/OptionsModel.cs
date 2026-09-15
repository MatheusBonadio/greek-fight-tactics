using System;
using Game.Core;

namespace Game.UI
{
    public enum OptionRow
    {
        Volume,
        TextSpeed,
        AssistedMode,
        Back
    }

    public sealed class OptionsModel
    {
        public const int RowCount = 4;
        public const string TitleKey = "options.title";
        public const string OnKey = "options.on";
        public const string OffKey = "options.off";
        public const int PercentPerVolumeStep = 100 / GameSettings.VolumeSteps;

        private const string LabelPrefix = "options.";
        private const string TextSpeedPrefix = "options.text_speed.";

        public OptionsModel(GameSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public event Action Changed;

        public GameSettings Settings { get; }

        public int Cursor { get; private set; }

        public OptionRow Selected => (OptionRow)Cursor;

        public static string LabelKey(OptionRow row) => LabelPrefix + UiTextKeys.SnakeCase(row.ToString());

        public static string TextSpeedKey(TextSpeed speed) => TextSpeedPrefix + UiTextKeys.SnakeCase(speed.ToString());

        public void Move(int delta)
        {
            Cursor = ((Cursor + delta) % RowCount + RowCount) % RowCount;
        }

        public void Point(int row)
        {
            if (row >= 0 && row < RowCount)
            {
                Cursor = row;
            }
        }

        public void Adjust(int delta)
        {
            switch (Selected)
            {
                case OptionRow.Volume:
                    Settings.AdjustVolume(delta);
                    break;
                case OptionRow.TextSpeed:
                    Settings.CycleTextSpeed(delta);
                    break;
                case OptionRow.AssistedMode:
                    Settings.AssistedMode = !Settings.AssistedMode;
                    break;
                default:
                    return;
            }

            Changed?.Invoke();
        }

        public bool Confirm()
        {
            if (Selected == OptionRow.Back)
            {
                return true;
            }

            Adjust(1);
            return false;
        }

        public int VolumePercent => Settings.VolumeStep * PercentPerVolumeStep;
    }
}
