using System.Linq;
using Game.Battle;
using Game.Content;
using Game.Core;
using Game.EditorTools;
using Game.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Tests.EditMode.UI
{
    public sealed class BattleHudPrefabTests
    {
        private const int MaxNavigationSteps = 32;

        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Prefab_WiresBarsMenuInputAndACameraCanvas()
        {
            var hud = InstantiateHud();

            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, hud.Canvas.renderMode);
            Assert.IsNotNull(hud.PlayerCoherence);
            Assert.IsNotNull(hud.OpponentCoherence);
            Assert.IsNotNull(hud.Audience);
            Assert.AreEqual(6, hud.Menu.ActionRows.Count);
            Assert.IsTrue(hud.Menu.ActionRows.All(row => row.GetComponent<MenuRowPointer>() != null));
            Assert.IsNotNull(host.GetComponent<BattleMenuInput>().Controls);
            Assert.IsNotNull(hud.Dialogue);
            Assert.IsFalse(hud.Dialogue.IsOpen);
        }

        [Test]
        public void CompendiumPrefab_WiresTheViewAndInput()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattleHudTools.CompendiumPrefabPath);
            Assert.IsNotNull(prefab, $"run BattleHudTools.BuildHud to create {BattleHudTools.CompendiumPrefabPath}");

            host = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var view = host.GetComponent<CompendiumView>();
            var catalog = AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath);
            var compendium = new FallacyCompendium(catalog.All.Select(fallacy => fallacy.Id));
            foreach (var fallacy in catalog.All)
            {
                compendium.Unlock(fallacy.Id);
            }

            view.Bind(compendium);

            Assert.IsNotNull(host.GetComponent<CompendiumInput>().Controls);
            Assert.AreEqual(BattleHudTools.CompendiumRows, view.Rows.Count);
            Assert.IsTrue(view.Icon.enabled);
            StringAssert.StartsWith($"{catalog.Count}/{catalog.Count}", view.Counter.text);
        }

        [Test]
        public void RefutingTheRightFallacy_DrainsTheOpponentBarThroughTheHud()
        {
            var hud = InstantiateHud();
            var catalog = AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath);
            var resolver = new BattleResolver();
            var state = BattleArena.CreateDuel(Act.One, Profile.Philosopher, 7, BattleArena.OpponentOpens);
            state.Apply(resolver.Resolve(Play.Argue(BattleArena.OpponentId, BattleArena.PlayerId, new Argument("Quem nasceu no Cerâmico não tem autoridade para falar de justiça na ágora.", "ad_hominem", new[] { "a", "b" }, 1, ArgumentTones.Mocking)), state));
            var controller = new BattleMenuController(new ActionMenuModel(state, resolver), catalog.RefutableIds);
            hud.Bind(state, controller);
            BattleMenuChoice? chosen = null;
            hud.Menu.ChoiceMade += choice => chosen = choice;

            Assert.AreEqual("CIRCULAR", hud.Menu.ActionRows[0].text);
            hud.Menu.HandleNavigate(Vector2.down);
            hud.Menu.HandleConfirm();
            for (var step = 0; step < MaxNavigationSteps && controller.SubmenuOptions[controller.SubmenuCursor] != "ad_hominem"; step++)
            {
                hud.Menu.HandleNavigate(Vector2.down);
            }

            hud.Menu.HandleConfirm();
            Assert.IsTrue(chosen.HasValue);

            state.Apply(resolver.Resolve(Play.Refute(BattleArena.PlayerId, BattleArena.OpponentId, chosen.Value.FallacyId), state));
            hud.Sync();

            Assert.Less(hud.OpponentCoherence.Bar.Target, BattleArena.OpponentMaxCoherence);
            Assert.IsTrue(hud.OpponentCoherence.Bar.IsAnimating);
            Assert.AreEqual(state.PlayerAudience, hud.Audience.Meter.TargetPlayerShare);
        }

        [TestCase(BattleHudTools.HudPrefabPath)]
        [TestCase(BattleHudTools.ExplorationDialoguePrefabPath)]
        public void DialogueBox_OffersChoicesThatTheMouseCanPick(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab, $"run BattleHudTools.BuildHud to create {prefabPath}");
            host = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var box = host.GetComponentInChildren<DialogueBoxView>(true);

            Assert.IsNotNull(box.ChoicePanel);
            Assert.IsFalse(box.ChoicePanel.gameObject.activeSelf);
            Assert.IsNotNull(box.ChoiceCursorRect);
            Assert.AreEqual(DialogueChoice.MaxOptions, box.ChoiceRows.Count);
            Assert.IsTrue(box.ChoiceRows.All(row => row.raycastTarget && row.GetComponent<PanelRowPointer>() != null));

            var picked = -1;
            box.ChoiceMade += index => picked = index;
            box.Show(new DialogueLine("Thrasybios", null, "Aulas comigo, três dracmas por semana. Aceita?"), new[] { "Aceitar as aulas", "Recusar" });
            box.Confirm();

            Assert.IsTrue(box.IsAwaitingChoice);
            Assert.IsTrue(box.ChoicePanel.gameObject.activeInHierarchy);
            Assert.AreEqual("Recusar", box.ChoiceRows[1].text);
            box.ChoiceRows[1].GetComponent<PanelRowPointer>().OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Left });

            Assert.AreEqual(1, picked);
            Assert.IsFalse(box.IsOpen);
        }

        private BattleHud InstantiateHud()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattleHudTools.HudPrefabPath);
            Assert.IsNotNull(prefab, $"run BattleHudTools.BuildHud to create {BattleHudTools.HudPrefabPath}");
            host = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            return host.GetComponent<BattleHud>();
        }
    }
}
