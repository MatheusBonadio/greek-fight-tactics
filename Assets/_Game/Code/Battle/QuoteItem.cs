namespace Game.Battle
{
    public readonly struct QuoteItem
    {
        public readonly string Id;
        public readonly int CoherenceDelta;
        public readonly int AudienceDelta;
        public readonly int Uses;

        public QuoteItem(string id, int coherenceDelta, int audienceDelta, int uses)
        {
            Id = id;
            CoherenceDelta = coherenceDelta;
            AudienceDelta = audienceDelta;
            Uses = uses;
        }
    }
}
