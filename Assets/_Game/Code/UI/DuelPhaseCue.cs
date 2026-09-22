using Game.Core;

namespace Game.UI
{
    public readonly struct DuelPhaseCue
    {
        public readonly string OpponentId;
        public readonly int Index;
        public readonly string PhaseId;
        public readonly string MusicId;
        public readonly Posture Posture;

        public DuelPhaseCue(string opponentId, int index, string phaseId, string musicId, Posture posture)
        {
            OpponentId = opponentId;
            Index = index;
            PhaseId = phaseId;
            MusicId = musicId;
            Posture = posture;
        }

        public override string ToString() => $"{OpponentId} {PhaseId} ({Index}) music {MusicId} posture {Posture}";
    }
}
