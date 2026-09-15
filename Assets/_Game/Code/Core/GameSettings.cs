using System;

namespace Game.Core
{
    public sealed class GameSettings
    {
        public const int VolumeSteps = 10;
        public const int DefaultVolumeStep = 8;
        public const float DefaultVolume = DefaultVolumeStep / (float)VolumeSteps;
        public const float SlowCharactersPerSecond = 15f;
        public const float NormalCharactersPerSecond = 30f;
        public const float FastCharactersPerSecond = 60f;

        private const int TextSpeedCount = 3;

        private int volumeStep = DefaultVolumeStep;

        public int VolumeStep => volumeStep;

        public float MasterVolume
        {
            get => volumeStep / (float)VolumeSteps;
            set => volumeStep = Math.Clamp((int)Math.Round(value * VolumeSteps), 0, VolumeSteps);
        }

        public TextSpeed TextSpeed { get; set; } = TextSpeed.Normal;

        public bool AssistedMode { get; set; }

        public float CharactersPerSecond => TextSpeed switch
        {
            TextSpeed.Slow => SlowCharactersPerSecond,
            TextSpeed.Fast => FastCharactersPerSecond,
            _ => NormalCharactersPerSecond
        };

        public void AdjustVolume(int steps)
        {
            volumeStep = Math.Clamp(volumeStep + steps, 0, VolumeSteps);
        }

        public void CycleTextSpeed(int delta)
        {
            TextSpeed = (TextSpeed)((((int)TextSpeed + delta) % TextSpeedCount + TextSpeedCount) % TextSpeedCount);
        }

        public GameSettings Clone() => new() { MasterVolume = MasterVolume, TextSpeed = TextSpeed, AssistedMode = AssistedMode };
    }
}
