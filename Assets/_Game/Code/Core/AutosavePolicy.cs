namespace Game.Core
{
    public static class AutosavePolicy
    {
        public static bool ShouldSave(GameMode from, GameMode to) =>
            to == GameMode.Exploration && from is GameMode.BattleOutro or GameMode.Dialogue or GameMode.Cutscene;
    }
}
