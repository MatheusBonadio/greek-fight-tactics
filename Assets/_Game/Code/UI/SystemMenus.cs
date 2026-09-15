namespace Game.UI
{
    public static class SystemMenus
    {
        public const string Continue = "continue";
        public const string NewGame = "new_game";
        public const string Options = "options";
        public const string Credits = "credits";
        public const string Quit = "quit";
        public const string Resume = "resume";
        public const string Save = "save";
        public const string Compendium = "compendium";
        public const string MainMenu = "main_menu";
        public const string PauseTitleKey = "system.pause_title";

        private const string LabelPrefix = "system.";

        public static string LabelKey(string id) => LabelPrefix + id;

        public static MenuListModel Main(bool hasSave) => new(new[]
        {
            Entry(Continue, hasSave),
            Entry(NewGame),
            Entry(Options),
            Entry(Credits),
            Entry(Quit)
        });

        public static MenuListModel Pause() => new(new[]
        {
            Entry(Resume),
            Entry(Save),
            Entry(Options),
            Entry(Compendium),
            Entry(MainMenu)
        });

        private static MenuEntry Entry(string id, bool enabled = true) => new(id, LabelKey(id), enabled);
    }
}
