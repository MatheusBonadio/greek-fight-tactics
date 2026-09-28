using Game.Core;

namespace Game.Battle
{
    public sealed class BattleRules
    {
        public const int MaxAudience = 100;
        public const int DefaultMaxMoraleShift = 9;

        public int TurnLimit { get; set; } = 50;

        public int MoveRange { get; set; } = 3;

        public int RefuteRange { get; set; } = 3;

        public int RefuteDamage { get; set; } = 25;

        public int RefuteAudienceGain { get; set; } = 10;

        public int CounterattackDamage { get; set; } = 12;

        public int MaieuticsRange { get; set; } = 2;

        public int MaieuticsDamage { get; set; } = 30;

        public int MaieuticsMissAudienceLoss { get; set; } = 10;

        public int MaieuticsMissCoherenceLoss { get; set; } = 8;

        public int MaieuticsTurnCost { get; set; } = 2;

        public int ConcedeRecovery { get; set; } = 15;

        public int ConcedeAudienceLoss { get; set; } = 10;

        public int WithdrawReputationPenalty { get; set; } = ReputationRules.WithdrawPenalty;

        public int SophistArgueAudienceShift { get; set; } = 8;

        public int PhilosopherArgueCoherenceDamage { get; set; } = 8;

        public int CitizenAdjacencyAudienceBonus { get; set; } = 2;

        public int CorneredBlockedNeighbours { get; set; } = 3;

        public int CorneredCoherencePenalty { get; set; } = 5;

        public int FlankRefuteBonus { get; set; } = 8;

        public int DiagramMaieuticsBonus { get; set; } = 10;

        public int ActTwoConcedeBonus { get; set; } = 10;

        public int ActTwoMaieuticsRangeBonus { get; set; } = 1;

        public int InversionUsesPerBattle { get; set; } = 1;

        public int ListeningUsesPerBattle { get; set; } = 1;

        public bool AudienceVictory { get; set; } = true;

        public int PhaseCounterDamage { get; set; } = 20;

        public int PhaseCounterAudienceGain { get; set; } = 10;

        public int MoraleRadius { get; set; } = 2;

        public int MoraleDamageStep { get; set; } = 3;

        public int MaxMoraleShift { get; set; } = DefaultMaxMoraleShift;

        public PhasedDuel Phases { get; set; }
    }
}
