namespace Game.UI
{
    public sealed class AudienceMeter
    {
        public const int MaxShare = 100;
        public const float NeutralShare = MaxShare / 2f;

        private readonly DrainingBar playerShare;

        public AudienceMeter(int initialPlayerShare)
        {
            playerShare = new DrainingBar(MaxShare, initialPlayerShare);
        }

        public int TargetPlayerShare => playerShare.Target;

        public float DisplayedPlayerShare => playerShare.Displayed;

        public float Balance => (playerShare.Displayed - NeutralShare) / NeutralShare;

        public bool IsAnimating => playerShare.IsAnimating;

        public void SetPlayerShare(int share) => playerShare.SetValue(share);

        public void Tick(float deltaSeconds) => playerShare.Tick(deltaSeconds);
    }
}
