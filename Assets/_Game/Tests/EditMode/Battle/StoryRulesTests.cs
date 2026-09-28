using System;
using System.IO;
using Game.Battle;
using Game.Content;
using Game.Core;
using Game.DevCommands;
using Game.EditorTools;
using Game.Grid;
using Game.Net;
using Game.UI;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.Battle
{
    public sealed class StoryRulesTests
    {
        private string directory;
        private GameObject host;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), $"gft-story-{Guid.NewGuid():N}");
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                Object.DestroyImmediate(host);
            }

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void DamarisOffer_AcceptingGrantsInversionDrachmasAndMenexenosAbsence()
        {
            var save = new SaveData();
            save.Inventory[DamarisOffer.DrachmasItem] = 3;

            DamarisOffer.Apply(save, DamarisChoice.Accepted);

            Assert.AreEqual(DamarisChoice.Accepted, save.DamarisBranch);
            Assert.AreEqual(DamarisOffer.EnabledValue, save.Flags[DamarisOffer.AbilityFlag(AbilityId.Inversion)]);
            Assert.AreEqual(203, save.Inventory[DamarisOffer.DrachmasItem]);
            Assert.AreEqual("3", save.Flags[DamarisOffer.MenexenoAbsentDaysFlag]);
            Assert.IsFalse(save.Flags.ContainsKey(DamarisOffer.MenexenoRespectFlag));
            Assert.Throws<InvalidOperationException>(() => DamarisOffer.Apply(save, DamarisChoice.Refused));
        }

        [Test]
        public void DamarisOffer_RefusingGrantsListeningAndMenexenosRespect()
        {
            var save = new SaveData();

            DamarisOffer.Apply(save, DamarisChoice.Refused);

            Assert.AreEqual(DamarisOffer.EnabledValue, save.Flags[DamarisOffer.AbilityFlag(AbilityId.Listening)]);
            Assert.AreEqual(DamarisOffer.EnabledValue, save.Flags[DamarisOffer.MenexenoRespectFlag]);
            Assert.IsFalse(save.Inventory.ContainsKey(DamarisOffer.DrachmasItem));
            Assert.Throws<ArgumentException>(() => DamarisOffer.Apply(new SaveData(), DamarisChoice.Undecided));
        }

        [TestCase(DamarisChoice.Accepted)]
        [TestCase(DamarisChoice.Refused)]
        public void DamarisBranch_SurvivesTheLocalDatabase(DamarisChoice choice)
        {
            var save = new SaveData { ActFlag = Act.Two, Timestamp = 9 };
            DamarisOffer.Apply(save, choice);

            var store = new LocalSaveStore(new SaveDatabase(directory), 2);
            store.Save(save);

            Assert.IsTrue(new LocalSaveStore(new SaveDatabase(directory), 2).TryLoad(out var loaded, out var error), error);
            Assert.AreEqual(choice, loaded.DamarisBranch);
            var ability = DamarisOffer.AbilityFlag(DamarisOffer.GrantedAbility(choice).Value);
            Assert.AreEqual(save.Flags[ability], loaded.Flags[ability]);
        }

        [TestCase("accepted", DamarisChoice.Accepted, DamarisTribunalStance.Betrays)]
        [TestCase("refused", DamarisChoice.Refused, DamarisTribunalStance.Defends)]
        public void OlderSavesLoadedBySaveLoad_DriveTheTribunalBranch(string branch, DamarisChoice expected, DamarisTribunalStance stance)
        {
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, $"{branch}.json");
            File.WriteAllText(file, $"{{\"actFlag\": 2, \"damarisBranch\": \"{branch}\", \"timestamp\": 5}}");

            var loaded = JObject.Parse(SaveCommands.SaveLoad(file, directory, 1));
            Assert.IsTrue((bool)loaded["success"], loaded.ToString());

            var store = new LocalSaveStore(new SaveDatabase(directory), 1);
            Assert.IsTrue(store.TryLoad(out var save, out var error), error);
            Assert.AreEqual(expected, save.DamarisBranch);
            Assert.AreEqual(string.Empty, save.Location);
            Assert.AreEqual(stance, TribunalScript.StanceFor(save.DamarisBranch));
        }

        [Test]
        public void Tribunal_DamarisBetraysOnlyOnTheThirdTurnAfterAcceptingTheOffer()
        {
            Assert.IsFalse(TribunalScript.DamarisBetraysOnTurn(DamarisChoice.Accepted, 2));
            Assert.IsTrue(TribunalScript.DamarisBetraysOnTurn(DamarisChoice.Accepted, TribunalScript.BetrayalTurn));
            Assert.IsFalse(TribunalScript.DamarisBetraysOnTurn(DamarisChoice.Refused, TribunalScript.BetrayalTurn));
            Assert.AreEqual(DamarisTribunalStance.Absent, TribunalScript.StanceFor(DamarisChoice.Undecided));
        }

        [TestCase(ActionId.Refute, TribunalEnding.Sophist, 340, true, true)]
        [TestCase(ActionId.Concede, TribunalEnding.Philosopher, 240, false, false)]
        [TestCase(ActionId.Maieutics, TribunalEnding.Master, 281, true, true)]
        public void Tribunal_TheFinalActionDecidesTheEndingAndTheCount(ActionId action, TribunalEnding expected, int acquittals, bool acquitted, bool menexenoAcquits)
        {
            Assert.IsTrue(TribunalScript.TryEndingFor(action, out var ending));
            Assert.AreEqual(expected, ending);

            var count = TribunalScript.CountFor(ending, 13);
            Assert.AreEqual(acquittals, count.AcquittalVotes);
            Assert.AreEqual(acquitted, count.Acquitted);
            Assert.AreEqual(menexenoAcquits, count.VoteAt(JurorCount.MenexenoIndex));
            Assert.IsFalse(TribunalScript.TryEndingFor(ActionId.Withdraw, out _));
        }

        [Test]
        public void TargetPreview_NeverShowsTheRefuteHighlightOnAParadox()
        {
            var (paradoxState, paradoxMenu) = OpenedDuel(Act.Two, null);
            var highlighter = new RangeHighlighter(paradoxState.Grid, new PathFinder());
            paradoxMenu.TrySelect(ActionId.Refute);

            Assert.AreEqual(HighlightKind.None, BattleTargetPreview.Apply(highlighter, paradoxState, paradoxMenu.Selected));
            Assert.IsEmpty(highlighter.Cells);

            paradoxMenu.TrySelect(ActionId.Maieutics);
            Assert.AreEqual(HighlightKind.Maieutics, BattleTargetPreview.Apply(highlighter, paradoxState, paradoxMenu.Selected));

            var (refutableState, refutableMenu) = OpenedDuel(Act.One, "ad_hominem");
            refutableMenu.TrySelect(ActionId.Refute);
            Assert.AreEqual(HighlightKind.Refute, BattleTargetPreview.Apply(new RangeHighlighter(refutableState.Grid, new PathFinder()), refutableState, refutableMenu.Selected));

            var uiText = AssetDatabase.LoadAssetAtPath<UiTextSO>(DataTools.UiTextPath);
            StringAssert.Contains("erro a apontar", uiText.Get(UiTextKeys.For(RejectionReason.ArgumentNotRefutable)));
        }

        [Test]
        public void HudPreview_FollowsTheMenuSelection()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattleHudTools.HudPrefabPath);
            host = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var hud = host.GetComponent<BattleHud>();
            var catalog = AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath);
            var (state, menu) = OpenedDuel(Act.Two, null);
            hud.Bind(state, new BattleMenuController(menu, catalog.RefutableIds));
            var highlighter = new RangeHighlighter(state.Grid, new PathFinder());

            hud.BindPreview(highlighter);
            Assert.AreEqual(menu.Selected.IsAvailable ? HighlightKind.Move : HighlightKind.None, highlighter.Kind);

            hud.Menu.HandleNavigate(Vector2.down);
            Assert.AreEqual(HighlightKind.None, highlighter.Kind);

            hud.Menu.HandleNavigate(Vector2.down);
            Assert.AreEqual(HighlightKind.Maieutics, highlighter.Kind);
        }

        private static (BattleState State, ActionMenuModel Menu) OpenedDuel(Act act, string fallacyId)
        {
            var resolver = new BattleResolver();
            var state = BattleArena.CreateDuel(act, Profile.Philosopher, 7, BattleArena.OpponentOpens);
            var argument = new Argument("Um grão de areia não é um monte, e somar um grão nunca faz o monte.", fallacyId, new[] { "um grão não é monte", "um grão não faz diferença" }, 1, ArgumentTones.Curious);
            state.Apply(resolver.Resolve(Play.Argue(BattleArena.OpponentId, BattleArena.PlayerId, argument), state));
            return (state, new ActionMenuModel(state, resolver));
        }
    }
}
