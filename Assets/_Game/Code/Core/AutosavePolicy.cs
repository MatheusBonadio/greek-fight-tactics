namespace Game.Core
{
    public static class AutosavePolicy
    {
        public static bool ShouldSave(GameMode from, GameMode to) =>
            to == GameMode.Exploration && (from == GameMode.BattleOutro || from == GameMode.Dialogue);
    }
}
