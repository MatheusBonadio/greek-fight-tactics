using Game.Battle;
using Game.Core;
using Game.UI;
using NUnit.Framework;

namespace Game.Tests.EditMode.UI
{
    public sealed class BattleMenuControllerTests
    {
        private const int RefuteRow = 1;
        private const int MaieuticsRow = 2;
        private const int ConcedeRow = 4;

        [Test]
        public void Refute_OpensTheFallacySubmenuAndReturnsTheChosenFallacy()
        {
            var controller = Controller(Refutable());
            controller.Point(RefuteRow);

            Assert.IsFalse(controller.TryConfirm(out _));
            Assert.AreEqual(BattleMenuLayer.Fallacies, controller.Layer);
            CollectionAssert.AreEqual(FallacyIds(), controller.SubmenuOptions);

            controller.Navigate(1);
            controller.Navigate(-2);

            Assert.IsTrue(controller.TryConfirm(out var choice));
            Assert.AreEqual(ActionId.Refute, choice.Action);
            Assert.AreEqual("false_dilemma", choice.FallacyId);
            Assert.AreEqual(BattleMenuChoice.NoPremise, choice.PremiseIndex);
            Assert.AreEqual(BattleMenuLayer.Actions, controller.Layer);
        }

        [Test]
        public void Maieutics_OpensThePremiseSubmenuAndReturnsTheChosenPremise()
        {
            var controller = Controller(Refutable());
            controller.Point(MaieuticsRow);

            Assert.IsFalse(controller.TryConfirm(out _));
            Assert.AreEqual(BattleMenuLayer.Premises, controller.Layer);
            Assert.AreEqual(2, controller.SubmenuOptions.Count);

            controller.Point(1);
            Assert.IsTrue(controller.TryConfirm(out var choice));
            Assert.AreEqual(ActionId.Maieutics, choice.Action);
            Assert.AreEqual(1, choice.PremiseIndex);
            Assert.IsNull(choice.FallacyId);
        }

        [Test]
        public void Cancel_LeavesASubmenuWithoutChoosing()
        {
            var controller = Controller(Refutable());
            controller.Point(RefuteRow);
            controller.TryConfirm(out _);

            Assert.IsTrue(controller.Cancel());
            Assert.AreEqual(BattleMenuLayer.Actions, controller.Layer);
            Assert.IsEmpty(controller.SubmenuOptions);
            Assert.IsFalse(controller.Cancel());
        }

        [Test]
        public void RefuteOnAParadox_StaysDisabledWithItsReason()
        {
            var controller = Controller(new Argument("Um grão de areia não é um monte, e somar um grão nunca faz o monte.", null, new[] { "um grão não é monte", "um grão não faz diferença" }, 1, ArgumentTones.Curious), Act.Two);
            controller.Point(RefuteRow);

            Assert.IsFalse(controller.TryConfirm(out _));
            Assert.AreEqual(BattleMenuLayer.Actions, controller.Layer);
            Assert.AreEqual(RejectionReason.ArgumentNotRefutable, controller.SelectedReason);
        }

        [Test]
        public void DirectActions_AreChosenImmediately()
        {
            var controller = Controller(Refutable());
            controller.Point(ConcedeRow);
            Assume.That(controller.Actions.Selected.IsAvailable);

            Assert.IsTrue(controller.TryConfirm(out var choice));
            Assert.AreEqual(ActionId.Concede, choice.Action);
        }

        [Test]
        public void Point_IgnoresRowsOutsideTheList()
        {
            var controller = Controller(Refutable());
            controller.Point(MaieuticsRow);

            controller.Point(99);
            controller.Point(-1);

            Assert.AreEqual(ActionId.Maieutics, controller.Actions.Selected.Action);
        }

        private static string[] FallacyIds() => new[] { "ad_hominem", "straw_man", "false_dilemma" };

        private static Argument Refutable() =>
            new("Quem nasceu no Cerâmico não tem autoridade para falar de justiça na ágora.", "ad_hominem", new[] { "Kleia é do Cerâmico", "Oleiros não entendem de justiça" }, 1, ArgumentTones.Mocking);

        private static BattleMenuController Controller(Argument argument, Act act = Act.One)
        {
            var state = BattleArena.CreateDuel(act, Profile.Philosopher, 7, BattleArena.OpponentOpens);
            var resolver = new BattleResolver();
            state.Apply(resolver.Resolve(Play.Argue(BattleArena.OpponentId, BattleArena.PlayerId, argument), state));
            return new BattleMenuController(new ActionMenuModel(state, resolver), FallacyIds());
        }
    }
}
