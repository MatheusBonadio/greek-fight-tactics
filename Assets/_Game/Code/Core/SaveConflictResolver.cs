namespace Game.Core
{
    public static class SaveConflictResolver
    {
        public static SaveData PickNewest(SaveData local, SaveData remote)
        {
            if (local == null)
            {
                return remote;
            }

            if (remote == null)
            {
                return local;
            }

            return remote.Timestamp > local.Timestamp ? remote : local;
        }
    }
}
