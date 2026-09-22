using System.Linq;
using Game.Battle;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode.Battle
{
    public sealed class RefuteTutorialTests
    {
        private const int Seed = 402;
        private const int SimulatedBattles = 60;
        private const int MaxTurns = 40;
        private const string Taught = "equivocation";
        private const string Wrong = "ad_hominem";

        private static readonly Argument Amphora = new(
            "Esta ânfora não é uma ânfora, pois ânfora é o vaso ritual das Panateneias. O que o homem comprou é só um vaso comum.",
            Taught,
            new[] { "Ânfora é apenas o vaso ritual das Panateneias", "Todo vaso de duas alças é chamado ânfora" },
            0,
            ArgumentTones.Mocking);

        private static readonly Argument Death = new(
            "O fim de uma coisa é a sua perfeição. A morte é o fim da vida. Logo, a morte é a perfeição da vida, e não há o que temer.",
            Taught,
            new[] { "O fim de uma coisa é a sua perfeição", "A morte é o fim da vida" },
            0,
            ArgumentTones.Cunning);

        private readonly BattleResolver resolver = new();

        [Test]
        public void GuidedBattle_ExplainsTheArgumentTheSubmenuAMissAndAHitThenLetsKleiaFinishAlone()
        {
            var tutorial = new RefuteTutorial();
            var state = Duel();
            var arguments = new ScriptedArgumentSource(new[] { Amphora, Death });
            var opponent = new TutorialOpponent();

            Assert.AreEqual(TutorialCue.Intro, tutorial.OnBattleStart());
            Assert.AreEqual(TutorialCue.None, tutorial.OnPlayerTurn(state), "there is nothing to explain before the first argument");

            Assert.AreEqual(TutorialCue.None, tutorial.OnOutcome(Apply(state, opponent.Decide(state, state.Opponent, arguments)), state));
            Assert.AreEqual(Amphora.Speech, state.CurrentArgument.Value.Speech, "the lesson always opens with the same argument");

            Assert.AreEqual(TutorialCue.Argument, tutorial.OnPlayerTurn(state));
            Assert.AreEqual(TutorialCue.Fallacies, tutorial.OnFallaciesOpened());
            Assert.AreEqual(TutorialCue.None, tutorial.OnFallaciesOpened(), "the submenu hint plays once per turn");

            var coherence = state.Player.Coherence;
            var miss = Apply(state, Play.Refute(state.Player.Id, state.Opponent.Id, Wrong));
            Assert.IsTrue(miss.Counterattack);
            Assert.AreEqual(coherence, state.Player.Coherence, "a wrong answer costs nothing while learning");
            Assert.AreEqual(TutorialCue.Miss, tutorial.OnOutcome(miss, state));

            Apply(state, opponent.Decide(state, state.Opponent, arguments));
            Assert.AreEqual(Death.Speech, state.CurrentArgument.Value.Speech);
            Assert.AreEqual(TutorialCue.None, tutorial.OnPlayerTurn(state), "the argument is explained only once");
            Assert.AreEqual(TutorialCue.Fallacies, tutorial.OnFallaciesOpened(), "the hint comes back on the next turn until Kleia gets it right");

            var hit = Apply(state, Play.Refute(state.Player.Id, state.Opponent.Id, Taught));
            Assert.IsTrue(hit.Hit);
            Assert.AreEqual(TutorialCue.Hit, tutorial.OnOutcome(hit, state));
            Assert.IsTrue(tutorial.IsLearned);

            for (var turn = 0; turn < MaxTurns && state.Status == BattleStatus.Ongoing; turn++)
            {
                Apply(state, opponent.Decide(state, state.Opponent, arguments));
                Assert.AreEqual(TutorialCue.None, tutorial.OnPlayerTurn(state));
                Assert.AreEqual(TutorialCue.None, tutorial.OnFallaciesOpened(), "after the first hit Kleia refutes on her own");
                Assert.AreEqual(TutorialCue.None, tutorial.OnOutcome(Apply(state, Play.Refute(state.Player.Id, state.Opponent.Id, state.CurrentArgument.Value.FallacyId)), state));
            }

            Assert.AreEqual(BattleStatus.PlayerWon, state.Status);
            Assert.AreEqual(TutorialCue.Victory, tutorial.OnBattleOver(state));
        }

        [Test]
        public void AnotherAction_RemindsKleiaToRefuteOnHerNextTurn()
        {
            var tutorial = new RefuteTutorial();
            var state = Duel();
            var arguments = new ScriptedArgumentSource(new[] { Amphora });
            var opponent = new TutorialOpponent();

            Apply(state, opponent.Decide(state, state.Opponent, arguments));
            Assert.AreEqual(TutorialCue.Argument, tutorial.OnPlayerTurn(state));
            Assert.AreEqual(TutorialCue.None, tutorial.OnOutcome(Apply(state, Play.Concede(state.Player.Id)), state));

            Apply(state, opponent.Decide(state, state.Opponent, arguments));
            Assert.AreEqual(TutorialCue.Reminder, tutorial.OnPlayerTurn(state));
            Assert.AreEqual(TutorialCue.None, tutorial.OnPlayerTurn(state), "the reminder plays once per detour");
        }

        [Test]
        public void OpponentMoves_AndRejectedPlays_DoNotAdvanceTheLesson()
        {
            var tutorial = new RefuteTutorial();
            var state = Duel();

            var rejected = resolver.Resolve(Play.Refute(state.Player.Id, state.Opponent.Id, Taught), state);
            Assert.IsTrue(rejected.IsRejected);
            Assert.AreEqual(TutorialCue.None, tutorial.OnOutcome(rejected, state));
            Assert.AreEqual(TutorialCue.None, tutorial.OnOutcome(null, state));
            Assert.IsFalse(tutorial.IsLearned);
        }

        [Test]
        public void LosingOrLeaving_DoesNotPlayTheVictoryScene()
        {
            var tutorial = new RefuteTutorial();
            var state = Duel();
            Apply(state, new TutorialOpponent().Decide(state, state.Opponent, new ScriptedArgumentSource(new[] { Amphora })));
            Apply(state, Play.Withdraw(state.Player.Id));

            Assert.AreEqual(BattleStatus.PlayerWithdrew, state.Status);
            Assert.AreEqual(TutorialCue.None, tutorial.OnBattleOver(state));
        }

        [Test]
        public void TutorialRules_OnlyForgiveMistakes()
        {
            var tutorial = RefuteTutorial.Rules();
            var standard = new BattleRules();

            Assert.AreEqual(0, tutorial.CounterattackDamage);
            Assert.AreEqual(0, tutorial.CorneredCoherencePenalty);
            Assert.AreEqual(standard.RefuteDamage, tutorial.RefuteDamage);
            Assert.AreEqual(standard.RefuteRange, tutorial.RefuteRange);
            Assert.AreEqual(standard.TurnLimit, tutorial.TurnLimit);
        }

        [Test]
        public void TutorialBattle_CannotBeLostWhateverKleiaDoes()
        {
            var fallacies = new[] { Taught, Wrong, "straw_man" };
            for (var battle = 0; battle < SimulatedBattles; battle++)
            {
                var state = Duel(Seed + battle);
                var result = BattleSimulator.Run(state, new RandomPlayerPolicy(new System.Random(Seed + battle), fallacies), new TutorialOpponent(), new ScriptedArgumentSource(new[] { Amphora, Death }));

                Assert.AreNotEqual(BattleStatus.PlayerLost, result.Status, $"battle {battle}");
                Assert.IsTrue(state.Log.All(entry => entry.PlayerCoherence == state.Player.MaxCoherence), $"battle {battle}: Kleia lost coherence during the tutorial");
            }

            var refuting = Duel();
            Assert.AreEqual(BattleStatus.PlayerWon, BattleSimulator.Run(refuting, new StrategicPlayerPolicy(), new TutorialOpponent(), new ScriptedArgumentSource(new[] { Amphora })).Status);
        }

        [Test]
        public void ScriptedArguments_AreServedInOrderAndRepeat()
        {
            var source = new ScriptedArgumentSource(new[] { Amphora, Death });
            var debater = Duel().Opponent;

            Assert.AreEqual(2, source.Count);
            Assert.IsTrue(source.TryNext(debater, out var first));
            Assert.IsTrue(source.TryNext(debater, out var second));
            Assert.IsTrue(source.TryNext(debater, out var third));
            Assert.AreEqual(Amphora.Speech, first.Speech);
            Assert.AreEqual(Death.Speech, second.Speech);
            Assert.AreEqual(Amphora.Speech, third.Speech);

            Assert.IsFalse(new ScriptedArgumentSource(null).TryNext(debater, out _));
        }

        [Test]
        public void TutorialOpponent_ArguesAtKleiaAndConcedesWhenItRunsOutOfArguments()
        {
            var state = Duel();
            var opponent = new TutorialOpponent();

            var argue = opponent.Decide(state, state.Opponent, new ScriptedArgumentSource(new[] { Amphora }));
            Assert.AreEqual(ActionId.Argue, argue.Action);
            Assert.AreEqual(state.Player.Id, argue.TargetId);
            Assert.AreEqual(Amphora.Speech, argue.PresentedArgument.Value.Speech);

            Assert.AreEqual(ActionId.Concede, opponent.Decide(state, state.Opponent, new ScriptedArgumentSource(null)).Action);
            Assert.AreEqual(ActionId.Concede, opponent.Decide(state, state.Opponent, null).Action);
        }

        private static BattleState Duel(int seed = Seed) => BattleArena.CreateDuel(Act.One, Profile.Sophist, seed, BattleArena.OpponentOpens, RefuteTutorial.Rules());

        private Outcome Apply(BattleState state, in Play play)
        {
            var outcome = resolver.Resolve(play, state);
            Assert.IsFalse(outcome.IsRejected, $"{play.Action} was rejected: {outcome.Rejection}");
            state.Apply(outcome);
            return outcome;
        }
    }
}
