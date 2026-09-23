namespace Game.Core
{
    public enum AudioBus
    {
        Music,
        Sfx,
        Ui
    }

    public static class AudioIds
    {
        public const string MusicTitle = "music_title";
        public const string MusicKerameikos = "music_kerameikos";
        public const string MusicAgora = "music_agora";
        public const string MusicArchive = "music_archive";
        public const string MusicAcademy = "music_academy";
        public const string MusicBattleSophist = "music_battle_sophist";
        public const string MusicBattlePhilosopher = "music_battle_philosopher";

        public const string Cursor = "sfx_cursor";
        public const string Confirm = "sfx_confirm";
        public const string Cancel = "sfx_cancel";
        public const string Refute = "sfx_refute";
        public const string Maieutics = "sfx_maieutics";
        public const string Damage = "sfx_damage";
        public const string Counter = "sfx_counter";
        public const string Victory = "sfx_victory";
        public const string Defeat = "sfx_defeat";
        public const string VoiceBlip = "voice_blip";

        public const string MusicPrefix = "music_";

        public static string MusicFor(Profile profile) =>
            profile == Profile.Philosopher ? MusicBattlePhilosopher : MusicBattleSophist;

        public static string MusicForLocation(string location) =>
            string.IsNullOrEmpty(location) ? null : MusicPrefix + location;

        public static string MusicForPhase(string musicId) =>
            string.IsNullOrEmpty(musicId) ? null : MusicPrefix + musicId;

        public static string SoundFor(ActionId action) => action switch
        {
            ActionId.Refute => Refute,
            ActionId.Maieutics => Maieutics,
            ActionId.Quote => Confirm,
            ActionId.Concede => Cancel,
            ActionId.Withdraw => Cancel,
            _ => null
        };

        public static AudioBus BusFor(string id) =>
            id != null && id.StartsWith(MusicPrefix, System.StringComparison.Ordinal) ? AudioBus.Music : AudioBus.Sfx;
    }
}
