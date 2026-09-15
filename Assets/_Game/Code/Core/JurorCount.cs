using System;

namespace Game.Core
{
    public sealed class JurorCount
    {
        public const int MenexenoIndex = TribunalVerdicts.Jurors - 1;
        public const float DefaultVotesPerSecond = 60f;
        public const float MenexenoPauseSeconds = 1.5f;

        private readonly bool[] votes = new bool[TribunalVerdicts.Jurors];
        private float clock;

        public JurorCount(int acquittalVotes, bool menexenoAcquits, int seed, float votesPerSecond = DefaultVotesPerSecond)
        {
            if (acquittalVotes < 0 || acquittalVotes > TribunalVerdicts.Jurors)
            {
                throw new ArgumentOutOfRangeException(nameof(acquittalVotes), acquittalVotes, "Acquittal votes must fit the jury.");
            }

            if ((menexenoAcquits && acquittalVotes == 0) || (!menexenoAcquits && acquittalVotes == TribunalVerdicts.Jurors))
            {
                throw new ArgumentException("Menexeno's vote must be one of the counted votes.", nameof(menexenoAcquits));
            }

            if (votesPerSecond <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(votesPerSecond), votesPerSecond, "The count needs a positive speed.");
            }

            AcquittalVotes = acquittalVotes;
            MenexenoAcquits = menexenoAcquits;
            VotesPerSecond = votesPerSecond;

            var otherAcquittals = acquittalVotes - (menexenoAcquits ? 1 : 0);
            for (var index = 0; index < MenexenoIndex; index++)
            {
                votes[index] = index < otherAcquittals;
            }

            Shuffle(new Random(seed));
            votes[MenexenoIndex] = menexenoAcquits;
        }

        public int AcquittalVotes { get; }

        public int CondemnationVotes => TribunalVerdicts.Jurors - AcquittalVotes;

        public bool MenexenoAcquits { get; }

        public float VotesPerSecond { get; }

        public int Revealed { get; private set; }

        public int AcquittalsRevealed { get; private set; }

        public int CondemnationsRevealed => Revealed - AcquittalsRevealed;

        public bool IsComplete => Revealed == TribunalVerdicts.Jurors;

        public bool IsWaitingForMenexeno => Revealed == MenexenoIndex;

        public bool Acquitted => AcquittalVotes >= TribunalVerdicts.Majority;

        public bool VoteAt(int index) => votes[index];

        public int Tick(float deltaSeconds)
        {
            if (IsComplete)
            {
                return 0;
            }

            clock += Math.Max(0f, deltaSeconds);
            var target = Math.Min(MenexenoIndex, (int)Math.Floor(clock * VotesPerSecond));
            if (target == MenexenoIndex && clock >= MenexenoIndex / VotesPerSecond + MenexenoPauseSeconds)
            {
                target = TribunalVerdicts.Jurors;
            }

            return RevealUntil(target);
        }

        public int SkipToEnd() => RevealUntil(TribunalVerdicts.Jurors);

        private int RevealUntil(int target)
        {
            var before = Revealed;
            while (Revealed < target)
            {
                if (votes[Revealed])
                {
                    AcquittalsRevealed++;
                }

                Revealed++;
            }

            return Revealed - before;
        }

        private void Shuffle(Random random)
        {
            for (var index = MenexenoIndex - 1; index > 0; index--)
            {
                var swap = random.Next(index + 1);
                (votes[index], votes[swap]) = (votes[swap], votes[index]);
            }
        }
    }
}
