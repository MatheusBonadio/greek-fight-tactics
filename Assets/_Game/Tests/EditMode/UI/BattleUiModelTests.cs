using System.Linq;
using Game.Battle;
using Game.Core;
using Game.UI;
using NUnit.Framework;

namespace Game.Tests.EditMode.UI
{
    public sealed class BattleUiModelTests
    {
        [Test]
        public void DrainingBar_AnimatesOverFourHundredMilliseconds()
        {
            var bar = new DrainingBar(100, 80);

            bar.SetValue(40);
            Assert.AreEqual(80f, bar.Displayed);

            bar.Tick(0.2f);
            Assert.AreEqual(60f, bar.Displayed, 0.001f);
            Assert.IsTrue(bar.IsAnimating);

            bar.Tick(0.3f);
            Assert.AreEqual(40f, bar.Displayed);
            Assert.IsFalse(bar.IsAnimating);
        }

        [Test]
        public void DrainingBar_FlashesForOneFrameOnlyWhenDamaged()
        {
            var bar = new DrainingBar(50, 50);

            bar.SetValue(30);
            Assert.IsTrue(bar.ConsumeFlashFrame());
            Assert.IsFalse(bar.ConsumeFlashFrame());

            bar.SetValue(45);
            Assert.IsFalse(bar.ConsumeFlashFrame());
        }

        [TestCase(26, false)]
        [TestCase(25, false)]
        [TestCase(24, true)]
        [TestCase(0, true)]
        public void DrainingBar_WarnsBelowAQuarter(int value, bool critical)
        {
            var bar = new DrainingBar(100, 100);

            bar.SetValue(value);

            Assert.AreEqual(critical, bar.IsCritical);
        }

        [Test]
        public void AudienceMeter_PullsTowardWhoeverConvinces()
        {
            var meter = new AudienceMeter(50);
            Assert.AreEqual(0f, meter.Balance);

            meter.SetPlayerShare(75);
            meter.Tick(DrainingBar.DrainSeconds);
            Assert.AreEqual(0.5f, meter.Balance, 0.001f);

            meter.SetPlayerShare(10);
            meter.Tick(DrainingBar.DrainSeconds);
            Assert.AreEqual(-0.8f, meter.Balance, 0.001f);
        }

        [Test]
        public void ActionMenu_ListsSixActionsAndWrapsTheCursor()
        {
            var state = BattleArena.CreateDuel(Act.One, Profile.Sophist, 402);
            var menu = new ActionMenuModel(state, new BattleResolver());

            CollectionAssert.AreEqual(ActionMenuModel.PlayerActions, menu.Entries.Select(entry => entry.Action));

            menu.MoveCursor(-1);
            Assert.AreEqual(ActionId.Withdraw, menu.Selected.Action);
            menu.MoveCursor(2);
            Assert.AreEqual(ActionId.Refute, menu.Selected.Action);
        }

        [Test]
        public void ActionMenu_DisablesRefuteWithAReasonWhenTheArgumentIsAParadox()
        {
            var state = OpenedDuel(Act.Two, new Argument("Um grão de areia não é um monte, e somar um grão nunca faz o monte.", null, new[] { "um grão não é monte", "um grão não faz diferença" }, 1, ArgumentTones.Curious));
            var menu = new ActionMenuModel(state, new BattleResolver());

            var refute = menu.Entries.First(entry => entry.Action == ActionId.Refute);
            Assert.IsFalse(refute.IsAvailable);
            Assert.AreEqual(RejectionReason.ArgumentNotRefutable, refute.Reason);

            Assert.IsTrue(menu.TrySelect(ActionId.Refute));
            Assert.IsFalse(menu.TryConfirm(out _));
            Assert.IsEmpty(menu.FallacyOptions(new[] { "ad_hominem" }));

            Assert.IsTrue(menu.TrySelect(ActionId.Maieutics));
            Assert.IsTrue(menu.TryConfirm(out var action));
            Assert.AreEqual(ActionId.Maieutics, action);
            Assert.AreEqual(2, menu.PremiseOptions().Count);
        }

        [Test]
        public void ActionMenu_OffersTheFallacySubmenuForRefutableArguments()
        {
            var state = OpenedDuel(Act.One, new Argument("Quem nasceu no Cerâmico não tem autoridade para falar de justiça.", "ad_hominem", new[] { "a", "b" }, 1, ArgumentTones.Mocking));
            var menu = new ActionMenuModel(state, new BattleResolver());

            Assert.IsTrue(menu.TrySelect(ActionId.Refute));
            Assert.IsTrue(menu.Selected.IsAvailable);
            CollectionAssert.AreEqual(new[] { "ad_hominem", "straw_man" }, menu.FallacyOptions(new[] { "ad_hominem", "straw_man" }));
        }

        [Test]
        public void Typewriter_RevealsAtTheConfiguredSpeedAndCanSkip()
        {
            var typewriter = new Typewriter { CharactersPerSecond = 10f };
            typewriter.Begin("Não sei.");

            typewriter.Tick(0.35f);
            Assert.AreEqual("Não", typewriter.VisibleText);
            Assert.IsFalse(typewriter.IsComplete);

            typewriter.Skip();
            Assert.AreEqual("Não sei.", typewriter.VisibleText);
            Assert.IsTrue(typewriter.IsComplete);

            typewriter.CharactersPerSecond = 0f;
            Assert.AreEqual(Typewriter.MinimumCharactersPerSecond, typewriter.CharactersPerSecond);
        }

        [Test]
        public void Compendium_TracksUnlockedFallaciesInCatalogOrder()
        {
            var compendium = new FallacyCompendium(new[] { "ad_hominem", "straw_man", "liar" });

            Assert.IsTrue(compendium.Unlock("liar"));
            Assert.IsTrue(compendium.Unlock("ad_hominem"));
            Assert.IsFalse(compendium.Unlock("liar"));
            Assert.IsFalse(compendium.Unlock("not_in_catalog"));

            CollectionAssert.AreEqual(new[] { "ad_hominem", "liar" }, compendium.UnlockedIds);
            Assert.AreEqual(3, compendium.Total);
        }

        [Test]
        public void AssistedMode_HighlightsTheWeakPremiseOnlyWhenEnabled()
        {
            var argument = new Argument("fala qualquer", "ad_hominem", new[] { "a", "b", "c" }, 2, ArgumentTones.Mocking);

            Assert.AreEqual(2, FallacyCompendium.WeakPremiseToHighlight(argument, true));
            Assert.IsNull(FallacyCompendium.WeakPremiseToHighlight(argument, false));
            Assert.IsNull(FallacyCompendium.WeakPremiseToHighlight(null, true));
        }

        private static BattleState OpenedDuel(Act act, Argument argument)
        {
            var state = BattleArena.CreateDuel(act, Profile.Philosopher, 7, BattleArena.OpponentOpens);
            state.Apply(new BattleResolver().Resolve(Play.Argue(BattleArena.OpponentId, BattleArena.PlayerId, argument), state));
            return state;
        }
    }
}
