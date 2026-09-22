namespace Game.UI
{
    public interface IStoryShortcut
    {
        string ReachedLabel { get; }

        string NextLabel { get; }

        bool SkipToNextCheckpoint();
    }
}
