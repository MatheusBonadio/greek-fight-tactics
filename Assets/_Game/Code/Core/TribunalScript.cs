namespace Game.Core
{
    public enum DamarisTribunalStance
    {
        Absent,
        Defends,
        Betrays
    }

    public enum TribunalEnding
    {
        Sophist,
        Philosopher,
        Master
    }

    public static class TribunalScript
    {
        public const int BetrayalTurn = 3;

        public static DamarisTribunalStance StanceFor(DamarisChoice choice) => choice switch
        {
            DamarisChoice.Accepted => DamarisTribunalStance.Betrays,
            DamarisChoice.Refused => DamarisTribunalStance.Defends,
            _ => DamarisTribunalStance.Absent
        };

        public static bool DamarisBetraysOnTurn(DamarisChoice choice, int turn) =>
            StanceFor(choice) == DamarisTribunalStance.Betrays && turn == BetrayalTurn;

        public static bool TryEndingFor(ActionId finalAction, out TribunalEnding ending)
        {
            switch (finalAction)
            {
                case ActionId.Refute:
                    ending = TribunalEnding.Sophist;
                    return true;
                case ActionId.Concede:
                    ending = TribunalEnding.Philosopher;
                    return true;
                case ActionId.Maieutics:
                    ending = TribunalEnding.Master;
                    return true;
                default:
                    ending = default;
                    return false;
            }
        }

        public static int AcquittalVotes(TribunalEnding ending) => ending switch
        {
            TribunalEnding.Sophist => TribunalVerdicts.SophistAcquittalVotes,
            TribunalEnding.Philosopher => TribunalVerdicts.Jurors - TribunalVerdicts.PhilosopherCondemnationVotes,
            _ => TribunalVerdicts.MasterAcquittalVotes
        };

        public static bool MenexenoAcquits(TribunalEnding ending) => ending != TribunalEnding.Philosopher;

        public static JurorCount CountFor(TribunalEnding ending, int seed) => new(AcquittalVotes(ending), MenexenoAcquits(ending), seed);
    }
}
