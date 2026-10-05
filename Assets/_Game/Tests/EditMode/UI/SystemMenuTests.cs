using System;
using System.Linq;
using Game.Content;
using Game.Core;
using Game.EditorTools;
using Game.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.UI
{
    public sealed class SystemMenuTests
    {
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
        public void MainMenu_OffersContinueOnlyWhenASaveExists()
        {
            var withoutSave = SystemMenus.Main(false);
            Assert.IsFalse(withoutSave.Entries[0].Enabled);
            Assert.AreEqual(SystemMenus.NewGame, withoutSave.Selected.Id);
            Assert.IsFalse(withoutSave.Point(0));

            withoutSave.Move(-1);
            Assert.AreEqual(SystemMenus.Quit, withoutSave.Selected.Id);
            withoutSave.Move(1);
            Assert.AreEqual(SystemMenus.NewGame, withoutSave.Selected.Id);

            var withSave = SystemMenus.Main(true);
            Assert.AreEqual(SystemMenus.Continue, withSave.Selected.Id);
            Assert.IsTrue(withSave.TryConfirm(out var id));
            Assert.AreEqual(SystemMenus.Continue, id);
        }

        [Test]
        public void MainMenu_PlaysTheSunsetBehindTheTitle()
        {
            var scene = EditorSceneManager.OpenScene(SystemMenuTools.MainMenuScenePath, OpenSceneMode.Additive);
            try
            {
                var flipbook = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SpriteFlipbook>(true)).Single();
                var paths = flipbook.Frames.Select(AssetDatabase.GetAssetPath).ToList();
                Assert.AreEqual(SpriteFlipbook.DefaultFramesPerSecond, flipbook.FramesPerSecond);
                Assert.Greater(paths.Count, 1, "the title screen needs more than the still painting");
                Assert.AreEqual($"{BattleHudTools.UiSpriteFolder}/{SystemMenuTools.TitleBackgroundSprite}.png", paths[0], "the painting opens the loop");
                CollectionAssert.AllItemsAreUnique(paths);
                Assert.IsTrue(flipbook.Frames.All(frame => frame), "every frame of the title screen must exist");

                var image = flipbook.GetComponent<Image>();
                Assert.AreSame(flipbook.Frames[0], image.sprite);
                flipbook.Advance(1f / SpriteFlipbook.DefaultFramesPerSecond);
                Assert.AreEqual(1, flipbook.Index);
                Assert.AreSame(flipbook.Frames[1], image.sprite);

                flipbook.Advance(flipbook.Frames.Count / SpriteFlipbook.DefaultFramesPerSecond);
                Assert.AreEqual(1, flipbook.Index, "the sunset loops back to where it started");

                var clouds = flipbook.GetComponentsInChildren<DriftingSprite>(true);
                Assert.AreEqual(SystemMenuTools.TitleClouds.Length, clouds.Length, "every cloud of the sky drifts");
                foreach (var (name, start, speed) in SystemMenuTools.TitleClouds)
                {
                    var cloud = clouds.Single(drifting => drifting.name == name);
                    Assert.AreEqual(start, cloud.Origin);
                    Assert.AreEqual(speed, cloud.PixelsPerSecond);
                    Assert.Greater(cloud.Origin.y, SystemMenuTools.TitleBackgroundHeight / 2f, "clouds belong in the sky, not in the city");
                    Assert.AreEqual($"{BattleHudTools.UiSpriteFolder}/{name}.png", AssetDatabase.GetAssetPath(cloud.GetComponent<Image>().sprite));
                }

                CollectionAssert.AllItemsAreUnique(clouds.Select(cloud => cloud.PixelsPerSecond).ToList(), "clouds at different heights cross at different speeds");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void MenuList_DisablingTheSelectedEntryMovesTheCursor()
        {
            var menu = SystemMenus.Pause();
            menu.Point(2);

            menu.SetEnabled(SystemMenus.Options, false);

            Assert.AreEqual(SystemMenus.Resume, menu.Selected.Id);
            Assert.Throws<ArgumentException>(() => new MenuListModel(Array.Empty<MenuEntry>()));
        }

        [Test]
        public void PausePrefab_CarriesAHiddenCompendiumPanelWiredToTheCatalog()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SystemMenuTools.PauseMenuPrefabPath);
            Assert.IsNotNull(prefab, $"run SystemMenuTools.BuildMenus to create {SystemMenuTools.PauseMenuPrefabPath}");
            host = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var pause = host.GetComponent<PauseMenuController>();

            Assert.IsNotNull(pause.Compendium, "the pause menu must carry its compendium panel");
            Assert.IsFalse(pause.Compendium.gameObject.activeSelf);
            Assert.AreEqual(BattleHudTools.CompendiumRows, pause.Compendium.Rows.Count);
            Assert.IsNull(pause.Compendium.GetComponent<CompendiumInput>(), "the pause menu input already drives the panel");
            Assert.IsTrue(pause.Compendium.Rows.All(row => row.GetComponent<PanelRowPointer>() != null));

            var catalog = AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath);
            var compendium = new FallacyCompendium(catalog.All.Select(fallacy => fallacy.Id));
            var first = catalog.All.First();
            compendium.Unlock(first.Id);
            pause.BindCompendium(pause.Compendium, compendium);

            Assert.AreEqual(first.DisplayName, pause.Compendium.Rows[0].text);
            Assert.AreEqual(first.DisplayName, pause.Compendium.Title.text);
        }

        [Test]
        public void Options_AdjustEveryRowAndBackCloses()
        {
            var settings = new GameSettings();
            var options = new OptionsModel(settings);
            var changes = 0;
            options.Changed += () => changes++;

            options.Adjust(1);
            Assert.AreEqual(90, options.VolumePercent);

            options.SetStep(OptionRow.Music, 3);
            Assert.AreEqual(30, options.PercentOf(OptionRow.Music));

            options.SetStep(OptionRow.Effects, 10);
            Assert.AreEqual(100, options.PercentOf(OptionRow.Effects));
            Assert.AreEqual(90, options.VolumePercent, "a volume row moved another row");

            options.Move((int)OptionRow.TextSpeed - options.Cursor);
            options.Adjust(-1);
            Assert.AreEqual(TextSpeed.Slow, settings.TextSpeed);

            options.Move(1);
            Assert.IsFalse(options.Confirm());
            Assert.IsTrue(settings.AssistedMode);

            options.Move(1);
            Assert.AreEqual(OptionRow.Back, options.Selected);
            Assert.IsTrue(options.Confirm());
            Assert.AreEqual(5, changes);

            options.Move(1);
            Assert.AreEqual(OptionRow.Volume, options.Selected);
        }

        [Test]
        public void GeneratedUiText_CoversSystemMenusOptionsAndCredits()
        {
            var uiText = UiText();
            var keys = new[] { SystemMenus.Continue, SystemMenus.NewGame, SystemMenus.Options, SystemMenus.Credits, SystemMenus.Quit, SystemMenus.Resume, SystemMenus.Compendium, SystemMenus.MainMenu }
                .Select(SystemMenus.LabelKey)
                .Concat(Enum.GetValues(typeof(OptionRow)).Cast<OptionRow>().Select(OptionsModel.LabelKey))
                .Concat(Enum.GetValues(typeof(TextSpeed)).Cast<TextSpeed>().Select(OptionsModel.TextSpeedKey))
                .Concat(new[] { OptionsModel.TitleKey, OptionsModel.OnKey, OptionsModel.OffKey, CreditsView.TitleKey, CreditsView.BodyKey, SystemMenus.PauseTitleKey });

            foreach (var key in keys)
            {
                Assert.IsTrue(uiText.Contains(key), key);
            }

            StringAssert.Contains("Matheus Bonadio", uiText.Get(CreditsView.BodyKey));
            StringAssert.Contains("Victor Vicentini", uiText.Get(CreditsView.BodyKey));
        }

        [Test]
        public void MenuListView_RendersLabelsFromDataDimsDisabledEntriesAndChoosesByPointer()
        {
            var uiText = UiText();
            host = new GameObject("menu", typeof(RectTransform));
            var rows = Enumerable.Range(0, 5).Select(index => Child<TextMeshProUGUI>($"row{index}")).Cast<TMP_Text>().ToArray();
            var view = host.AddComponent<MenuListView>();
            view.Configure(Child<TextMeshProUGUI>("title"), SystemMenus.PauseTitleKey, rows, Child<Image>("cursor").rectTransform, uiText);
            string chosen = null;
            view.Chosen += id => chosen = id;

            view.Bind(SystemMenus.Main(false));

            Assert.AreEqual(uiText.Get(SystemMenus.LabelKey(SystemMenus.Continue)), rows[0].text);
            Assert.AreEqual(view.UnavailableColor, rows[0].color);

            view.HandlePoint(0);
            view.HandleConfirm();
            Assert.AreEqual(SystemMenus.NewGame, chosen);

            view.HandlePoint(3);
            view.HandleConfirm();
            Assert.AreEqual(SystemMenus.Credits, chosen);
            Assert.IsTrue(view.Cursor.gameObject.activeSelf);
        }

        [Test]
        public void OptionsView_ShowsValuesFromDataAndAppliesHorizontalInput()
        {
            var uiText = UiText();
            host = new GameObject("options", typeof(RectTransform));
            var labels = Enumerable.Range(0, OptionsModel.RowCount).Select(index => Child<TextMeshProUGUI>($"label{index}")).Cast<TMP_Text>().ToArray();
            var values = Enumerable.Range(0, OptionsModel.RowCount).Select(index => Child<TextMeshProUGUI>($"value{index}")).Cast<TMP_Text>().ToArray();
            var view = host.AddComponent<OptionsView>();
            view.Configure(Child<TextMeshProUGUI>("title"), labels, values, Child<Image>("cursor").rectTransform, uiText);
            GameSettings applied = null;
            var closed = false;
            view.SettingsChanged += settings => applied = settings;
            view.Closed += () => closed = true;

            view.Bind(new OptionsModel(new GameSettings()));
            Assert.AreEqual(uiText.Get(OptionsModel.LabelKey(OptionRow.Volume)), labels[0].text);
            Assert.AreEqual("80%", values[0].text);

            view.HandleNavigate(Vector2.right);
            Assert.AreEqual("90%", values[0].text);
            Assert.IsNotNull(applied);

            view.HandlePoint((int)OptionRow.Music);
            view.HandleNavigate(Vector2.left);
            Assert.AreEqual("70%", values[(int)OptionRow.Music].text);
            Assert.AreEqual("90%", values[(int)OptionRow.Volume].text, "the music row moved the master");

            view.HandlePoint((int)OptionRow.TextSpeed);
            view.HandleNavigate(Vector2.right);
            Assert.AreEqual(uiText.Get(OptionsModel.TextSpeedKey(TextSpeed.Fast)), values[(int)OptionRow.TextSpeed].text);

            view.HandlePoint((int)OptionRow.AssistedMode);
            view.HandleConfirm();
            Assert.AreEqual(uiText.Get(OptionsModel.OnKey), values[(int)OptionRow.AssistedMode].text);

            view.HandleCancel();
            Assert.IsTrue(closed);
        }

        [Test]
        public void CreditsView_PagesThroughTheTeamAndCloses()
        {
            var uiText = UiText();
            host = new GameObject("canvas", typeof(RectTransform));
            host.AddComponent<Canvas>();
            var panel = new GameObject("credits", typeof(RectTransform));
            panel.transform.SetParent(host.transform, false);
            var body = Child<TextMeshProUGUI>("body", panel.transform);
            body.rectTransform.sizeDelta = new Vector2(192f, 16f);
            body.font = TMP_Settings.defaultFontAsset;
            body.fontSize = BattleHudTools.FontSize;
            body.textWrappingMode = TextWrappingModes.Normal;
            var view = panel.AddComponent<CreditsView>();
            view.Configure(Child<TextMeshProUGUI>("title", panel.transform), body, uiText);
            var closed = false;
            view.Closed += () => closed = true;

            view.Show();
            Assert.Greater(view.PageCount, 1);
            Assert.AreEqual(1, view.Page);

            view.HandleNavigate(Vector2.down);
            Assert.AreEqual(2, view.Page);
            view.HandleNavigate(Vector2.up);
            Assert.AreEqual(1, view.Page);

            for (var step = 0; step <= view.PageCount && !closed; step++)
            {
                view.HandleConfirm();
            }

            Assert.IsTrue(closed);
            Assert.IsFalse(panel.activeSelf);
        }

        private T Child<T>(string name, Transform parent = null) where T : Component
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent ? parent : host.transform, false);
            return child.AddComponent<T>();
        }

        private static UiTextSO UiText()
        {
            var uiText = AssetDatabase.LoadAssetAtPath<UiTextSO>(DataTools.UiTextPath);
            Assert.IsNotNull(uiText);
            return uiText;
        }
    }
}
