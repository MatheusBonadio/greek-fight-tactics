using System;
using System.Collections.Generic;
using Game.Core;
using Game.Grid;

namespace Game.Battle
{
    public sealed class OpponentBrain
    {
        public const double ActOneMistakeChance = 0.25;
        public const double SophistLowCoherenceRatio = 0.3;
        public const double PhilosopherLowCoherenceRatio = 0.5;

        private const int OpeningArgumentScore = 30;
        private const int ArgueScore = 10;
        private const int AudienceNeighbourScore = 8;
        private const int CorneredEscapeScore = 12;
        private const int ConcedeWhenLowScore = 15;
        private const int ConcedeWhenHealthyScore = -5;
        private const int MoveCostScore = -3;

        private readonly Random random;
        private readonly BattleResolver resolver = new();
        private readonly PathFinder pathFinder = new();
        private readonly List<Coord> reachable = new();
        private readonly List<Candidate> candidates = new();

        public OpponentBrain(Random random)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public Play Decide(BattleState state, Debater self, IArgumentSource arguments)
        {
            candidates.Clear();

            if (arguments != null && arguments.TryNext(self, out var argument))
            {
                Consider(state, Play.Argue(self.Id, state.Player.Id, argument), ScoreArgue(state, self));
            }

            Consider(state, Play.Concede(self.Id), ScoreConcede(self));

            pathFinder.Reachable(state.Grid, self.Position, state.Rules.MoveRange, reachable);
            foreach (var cell in reachable)
            {
                Consider(state, Play.Circulate(self.Id, cell), ScoreMove(state, self, cell));
            }

            if (candidates.Count == 0)
            {
                return Play.Concede(self.Id);
            }

            if (state.Act == Act.One && random.NextDouble() < ActOneMistakeChance)
            {
                return candidates[random.Next(candidates.Count)].Play;
            }

            var best = candidates[0];
            foreach (var candidate in candidates)
            {
                if (candidate.Score > best.Score)
                {
                    best = candidate;
                }
            }

            return best.Play;
        }

        private void Consider(BattleState state, Play play, int score)
        {
            if (!resolver.Resolve(play, state).IsRejected)
            {
                candidates.Add(new Candidate(play, score));
            }
        }

        private static int ScoreArgue(BattleState state, Debater self)
        {
            var baseScore = state.CurrentArgument.HasValue ? ArgueScore : OpeningArgumentScore;
            var crowdScore = self.Profile == Profile.Sophist
                ? PositionalModifiers.AdjacentAudienceMembers(state, self.Position) * AudienceNeighbourScore
                : 0;
            return baseScore + crowdScore;
        }

        private static int ScoreConcede(Debater self)
        {
            var threshold = self.Profile == Profile.Sophist ? SophistLowCoherenceRatio : PhilosopherLowCoherenceRatio;
            var ratio = (double)self.Coherence / self.MaxCoherence;
            return ratio < threshold ? ConcedeWhenLowScore : ConcedeWhenHealthyScore;
        }

        private static int ScoreMove(BattleState state, Debater self, Coord destination)
        {
            var escapeScore = PositionalModifiers.IsCornered(state, self.Position) && !PositionalModifiers.IsCornered(state, destination)
                ? CorneredEscapeScore
                : 0;

            if (self.Profile != Profile.Sophist)
            {
                return escapeScore + MoveCostScore;
            }

            var crowdGain = PositionalModifiers.AdjacentAudienceMembers(state, destination) - PositionalModifiers.AdjacentAudienceMembers(state, self.Position);
            return crowdGain * AudienceNeighbourScore + escapeScore + MoveCostScore;
        }

        private readonly struct Candidate
        {
            public readonly Play Play;
            public readonly int Score;

            public Candidate(Play play, int score)
            {
                Play = play;
                Score = score;
            }
        }
    }
}
