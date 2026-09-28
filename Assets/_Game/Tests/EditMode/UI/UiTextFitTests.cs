using System;
using System.Collections.Generic;
using System.Linq;
using Game.Battle;
using Game.Content;
using Game.Core;
using Game.EditorTools;
using Game.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.UI
{
    public sealed class UiTextFitTests
    {
        private const string WidestCoherence = "100/100";
        private const string WidestShare = "100%";

        private readonly List<GameObject> hosts = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var host in hosts.Where(host => host != null))
            {
                Object.DestroyImmediate(host);
            }

            hosts.Clear();
        }

        [Test]
        public void BattleHud_FitsEveryActionFallacyReasonAndNumberWithoutCuttingIt()
        {
            var hud = Instantiate(BattleHudTools.HudPrefabPath).GetComponent<BattleHud>();
            var uiText = UiText();
            var catalog = AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath);

            AssertFits(hud.Menu.ActionRows[0], Enum.GetValues(typeof(ActionId)).Cast<ActionId>().Where(action => action != ActionId.Argue).Select(action => uiText.Get(UiTextKeys.For(action))));
            AssertFits(hud.Menu.SubmenuRows[0], catalog.All.Select(fallacy => fallacy.DisplayName));
            AssertFits(hud.Menu.SubmenuTitle, new[] { uiText.Get(UiTextKeys.ChooseFallacy), uiText.Get(UiTextKeys.ChoosePremise) });
            AssertFits(hud.Menu.ReasonLabel, Enum.GetValues(typeof(RejectionReason)).Cast<RejectionReason>().Where(reason => reason != RejectionReason.None).Select(reason => uiText.Get(UiTextKeys.For(reason))));
            AssertFits(hud.PlayerPanel.ValueLabel, new[] { WidestCoherence });
            AssertFits(hud.OpponentPanel.ValueLabel, new[] { WidestCoherence });
            var widestMorale = new[] { hud.PlayerPanel.MoraleText(BattleRules.DefaultMaxMoraleShift), hud.PlayerPanel.MoraleText(-BattleRules.DefaultMaxMoraleShift) };
            AssertFits(hud.PlayerPanel.MoraleLabel, widestMorale);
            AssertFits(hud.OpponentPanel.MoraleLabel, widestMorale);
            AssertFits(hud.Audience.PlayerShareLabel, new[] { WidestShare });
            AssertFits(hud.Audience.OpponentShareLabel, new[] { WidestShare });
        }

        [Test]
        public void BattleHud_FitsTheLongestChallengerNameBesideTheCaption()
        {
            var hud = Instantiate(BattleHudTools.HudPrefabPath).GetComponent<BattleHud>();
            var panel = hud.OpponentPanel;
            var caption = panel.transform.Find("Caption").GetComponent<TMP_Text>();
            var opponents = AssetDatabase.FindAssets($"t:{nameof(OpponentSO)}")
                .Select(guid => AssetDatabase.LoadAssetAtPath<OpponentSO>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToList();
            Assert.IsNotEmpty(opponents);

            var captionWidth = caption.GetPreferredValues(caption.text).x;
            foreach (var opponent in opponents)
            {
                var plate = UiPlate.WidthFor(panel.NameLabel, opponent.DisplayName.ToUpperInvariant(), panel.NameLabel.GetComponentInParent<UiPlate>().Padding);
                var room = BattleHudTools.BadgeWidth - BattleHudTools.BadgeContent - 4f - captionWidth;
                Assert.LessOrEqual(plate, room, opponent.DisplayName);
            }
        }

        [Test]
        public void SystemMenus_FitEveryEntryOptionAndSlotLine()
        {
            var pause = Instantiate(SystemMenuTools.PauseMenuPrefabPath).GetComponent<PauseMenuController>();
            var uiText = UiText();
            var catalog = AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath);

            AssertFits(pause.PauseMenu.Rows[0], SystemMenus.Pause().Entries.Select(entry => uiText.Get(entry.LabelKey)));
            AssertFits(pause.Options.Labels[0], Enum.GetValues(typeof(OptionRow)).Cast<OptionRow>().Select(row => uiText.Get(OptionsModel.LabelKey(row))));
            AssertFits(pause.Options.Values[0], Enum.GetValues(typeof(TextSpeed)).Cast<TextSpeed>().Select(speed => uiText.Get(OptionsModel.TextSpeedKey(speed))).Concat(new[] { uiText.Get(OptionsModel.OnKey), uiText.Get(OptionsModel.OffKey), "100%" }));
            AssertFits(pause.Slots.Rows[0], new[] { LongestSlotLine(uiText) });
            AssertFits(pause.Compendium.Rows[0], catalog.All.Select(fallacy => fallacy.DisplayName));
        }

        [Test]
        public void MainMenu_FitsEveryEntryInsideItsBox()
        {
            var scene = EditorSceneManager.OpenScene(SystemMenuTools.MainMenuScenePath, OpenSceneMode.Additive);
            try
            {
                var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
                AtReferenceSize(menu.gameObject);
                var uiText = UiText();

                AssertFits(menu.MainMenu.Rows[0], SystemMenus.Main(true).Entries.Select(entry => uiText.Get(entry.LabelKey)));
                AssertFits(menu.Slots.Rows[0], new[] { LongestSlotLine(uiText) });
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void AssertFits(TMP_Text label, IEnumerable<string> texts, RectTransform container = null)
        {
            var width = container ? container.rect.width : label.rectTransform.rect.width;
            Assert.AreEqual(BattleHudTools.FontSize, label.fontSize, label.name);
            Assert.IsFalse(label.enableAutoSizing, $"{label.name} shrinks text instead of fitting it");
            foreach (var text in texts)
            {
                Assert.LessOrEqual(label.GetPreferredValues(text).x, width, $"{label.name} cuts \"{text}\"");
            }
        }

        private static string LongestSlotLine(UiTextSO uiText) =>
            string.Format(
                uiText.Get(SaveSlotText.SummaryLineKey),
                string.Format(uiText.Get(SaveSlotText.SlotKey), SaveSlots.Count),
                uiText.Get(SaveSlotText.ActKey(Act.Two)),
                uiText.Get(SaveSlotText.LocationKey("kerameikos")),
                SaveSlotText.Playtime(99 * 3600 + 59 * 60, uiText));

        private static UiTextSO UiText()
        {
            var uiText = AssetDatabase.LoadAssetAtPath<UiTextSO>(DataTools.UiTextPath);
            Assert.IsNotNull(uiText);
            return uiText;
        }

        private GameObject Instantiate(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab, $"run the UI tools to create {prefabPath}");
            var host = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            hosts.Add(host);
            AtReferenceSize(host);
            return host;
        }

        private static void AtReferenceSize(GameObject canvasRoot)
        {
            ((RectTransform)canvasRoot.transform).sizeDelta = new Vector2(BattleHudTools.CanvasWidth, BattleHudTools.CanvasHeight);
        }
    }
}
