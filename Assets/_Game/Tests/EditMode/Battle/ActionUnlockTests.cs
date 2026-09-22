using System;
using System.Linq;
using Game.Battle;
using Game.Content;
using Game.Core;
using Game.EditorTools;
using Game.Tests.EditMode.Grid;
using Game.UI;
using NUnit.Framework;
using UnityEditor;
using static Game.Tests.EditMode.Battle.BattleFactory;

namespace Game.Tests.EditMode.Battle
{
    public sealed class ActionUnlockTests
    {
        private static readonly ActionId[] Maieutics = { ActionId.Maieutics };

        [Test]
        public void Maieutics_StaysLockedUntilItsStoryFlagIsSet()
        {
            var save = new SaveData();

            Assert.IsFalse(ActionUnlocks.IsUnlocked(ActionId.Maieutics, save));
            Assert.IsFalse(ActionUnlocks.IsUnlocked(ActionId.Maieutics, null));
            CollectionAssert.AreEqual(Maieutics, ActionUnlocks.LockedIn(save, ActionMenuModel.PlayerActions));

            Assert.IsTrue(new StoryEffect(ActionUnlocks.MaieuticsFlag).TryApply(save, out _));
            Assert.IsTrue(ActionUnlocks.IsUnlocked(ActionId.Maieutics, save));
            CollectionAssert.IsEmpty(ActionUnlocks.LockedIn(save, ActionMenuModel.PlayerActions));
        }

        [Test]
        public void EveryOtherPlayerAction_NeedsNoFlag()
        {
            foreach (var action in ActionMenuModel.PlayerActions.Where(action => action != ActionId.Maieutics))
            {
                Assert.IsNull(ActionUnlocks.FlagFor(action), action.ToString());
                Assert.IsTrue(ActionUnlocks.IsUnlocked(action, new SaveData()), action.ToString());
            }

            Assert.IsTrue(SaveFlags.IsCustomName(ActionUnlocks.MaieuticsFlag));
        }

        [Test]
        public void LockedMaieutics_IsRejectedWithoutSpendingTheTurn()
        {
            var resolver = new BattleResolver();
            var state = CreateWithArgument(RefutableArgument(), lockedActions: Maieutics);
            var turn = state.Turn;

            var outcome = resolver.Resolve(Play.Maieutics(PlayerId, OpponentId, WeakPremise), state);
            state.Apply(outcome);

            Assert.AreEqual(RejectionReason.ActionLocked, outcome.Rejection);
            Assert.AreEqual(turn, state.Turn);
            Assert.AreEqual(PlayerId, state.Queue.Current);
            Assert.IsTrue(state.IsLockedForPlayer(ActionId.Maieutics));

            var refute = resolver.Resolve(Play.Refute(PlayerId, OpponentId, Fallacy), state);
            Assert.IsFalse(refute.IsRejected, "only the locked action is refused");
        }

        [Test]
        public void UnlockedMaieutics_ResolvesAsUsual()
        {
            var state = CreateWithArgument(RefutableArgument());

            var outcome = new BattleResolver().Resolve(Play.Maieutics(PlayerId, OpponentId, WeakPremise), state);

            Assert.IsFalse(outcome.IsRejected);
            Assert.IsTrue(outcome.Hit);
            CollectionAssert.IsEmpty(state.LockedPlayerActions);
        }

        [Test]
        public void Menu_ShowsMaieuticsUnavailableWithThePortugueseReason()
        {
            var locked = new ActionMenuModel(CreateWithArgument(RefutableArgument(), lockedActions: Maieutics), new BattleResolver());
            var unlocked = new ActionMenuModel(CreateWithArgument(RefutableArgument()), new BattleResolver());

            var entry = locked.Entries.Single(candidate => candidate.Action == ActionId.Maieutics);
            Assert.IsFalse(entry.IsAvailable);
            Assert.AreEqual(RejectionReason.ActionLocked, entry.Reason);
            Assert.IsTrue(locked.Entries.Single(candidate => candidate.Action == ActionId.Refute).IsAvailable);
            Assert.IsTrue(locked.TrySelect(ActionId.Maieutics));
            Assert.IsFalse(locked.TryConfirm(out _));
            Assert.IsTrue(unlocked.Entries.Single(candidate => candidate.Action == ActionId.Maieutics).IsAvailable);

            var uiText = AssetDatabase.LoadAssetAtPath<UiTextSO>(DataTools.UiTextPath);
            Assert.IsNotNull(uiText, $"run DataTools.Regenerate to create {DataTools.UiTextPath}");
            Assert.IsTrue(uiText.Contains(UiTextKeys.For(RejectionReason.ActionLocked)));
            StringAssert.DoesNotStartWith(UiTextSO.MissingPrefix, uiText.Get(UiTextKeys.For(RejectionReason.ActionLocked)));
        }

        [TestCase(BattleStatus.PlayerWon, DuelStoryFlags.Won)]
        [TestCase(BattleStatus.PlayerLost, DuelStoryFlags.Lost)]
        [TestCase(BattleStatus.PlayerWithdrew, DuelStoryFlags.Withdrew)]
        [TestCase(BattleStatus.TurnLimitReached, DuelStoryFlags.Stalemate)]
        public void DuelResult_IsAStoryFlagPerOpponent(BattleStatus status, string value)
        {
            var save = new SaveData();
            var effect = DuelStoryResults.ResultOf("thrasybios", status);

            Assert.IsTrue(effect.TryApply(save, out var error), error);
            Assert.AreEqual(value, save.Flags["duel_thrasybios"]);
            Assert.IsTrue(StoryCondition.Has(DuelStoryFlags.FlagFor("thrasybios")).IsMet(save));
            Assert.IsFalse(StoryCondition.Has(DuelStoryFlags.FlagFor("oron")).IsMet(save));
        }

        [Test]
        public void DuelResult_NeedsAnOpponentAndAFinishedBattle()
        {
            Assert.Throws<ArgumentException>(() => DuelStoryFlags.FlagFor(" "));
            Assert.Throws<ArgumentOutOfRangeException>(() => DuelStoryResults.ValueFor(BattleStatus.Ongoing));
            Assert.IsTrue(DataTools.OpponentIds().All(id => SaveFlags.IsCustomName(DuelStoryFlags.FlagFor(id))), "every opponent id makes a valid story flag");
        }

        [Test]
        public void SaveFromBeforeDuelFlags_ReadsAWonDuelRecordAsItsStoryFlag()
        {
            var save = new SaveData();
            save.Duels["thrasybios"] = new DuelRecord(true, DuelVictory.Refute);
            save.Duels["oron"] = new DuelRecord(false, DuelVictory.Concede);
            var wonThrasybios = new StoryCondition(DuelStoryFlags.FlagFor("thrasybios"), DuelStoryFlags.Won);

            Assert.IsTrue(wonThrasybios.IsMet(save), "a save that beat Thrasybios before duel flags existed still counts the win");
            Assert.IsFalse(StoryCondition.Has(DuelStoryFlags.FlagFor("oron")).IsMet(save), "only won duels stand in for the flag");
            Assert.IsFalse(StoryCondition.Has(DuelStoryFlags.FlagFor("simo")).IsMet(save));
            Assert.IsFalse(StoryCondition.Lacks(DuelStoryFlags.FlagFor("thrasybios")).IsMet(save));
            Assert.IsFalse(save.Flags.ContainsKey(DuelStoryFlags.FlagFor("thrasybios")), "reading does not write the flag");

            Assert.IsTrue(DuelStoryResults.ResultOf("thrasybios", BattleStatus.PlayerWon).TryApply(save, out _));
            Assert.IsTrue(wonThrasybios.IsMet(save));
        }

        [Test]
        public void Session_LocksTheActionsItsSetupNames()
        {
            var session = BattleSession.Start(GridFactory.Open(8, 8), new BattleSetup
            {
                PlayerId = PlayerId,
                PlayerCell = PlayerStart,
                OpponentId = OpponentId,
                OpponentCell = OpponentStart,
                LockedActions = Maieutics
            }, new ListArgumentSource(new[] { RefutableArgument() }, 1));

            Assert.IsTrue(session.State.IsLockedForPlayer(ActionId.Maieutics));
            Assert.IsFalse(session.State.IsLockedForPlayer(ActionId.Refute));
        }
    }
}
