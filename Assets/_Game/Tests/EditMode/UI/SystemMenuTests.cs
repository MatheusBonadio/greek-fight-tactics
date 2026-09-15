using System;
using System.Linq;
using Game.Content;
using Game.Core;
using Game.EditorTools;
using Game.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
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
        public void MenuList_DisablingTheSelectedEntryMovesTheCursor()
        {
            var menu = SystemMenus.Pause();
            menu.Point(1);

            menu.SetEnabled(SystemMenus.Options, false);

            Assert.AreEqual(SystemMenus.Resume, menu.Selected.Id);
            Assert.Throws<ArgumentException>(() => new MenuListModel(Array.Empty<MenuEntry>()));
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

            options.Move(1);
            options.Adjust(-1);
            Assert.AreEqual(TextSpeed.Slow, settings.TextSpeed);

            options.Move(1);
            Assert.IsFalse(options.Confirm());
            Assert.IsTrue(settings.AssistedMode);

            options.Move(1);
            options.Adjust(1);
            Assert.IsTrue(options.Confirm());
            Assert.AreEqual(3, changes);

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

            StringAssert.Contains("Open Font License", uiText.Get(CreditsView.BodyKey));
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
            var labels = Enumerable.Range(0, 4).Select(index => Child<TextMeshProUGUI>($"label{index}")).Cast<TMP_Text>().ToArray();
            var values = Enumerable.Range(0, 4).Select(index => Child<TextMeshProUGUI>($"value{index}")).Cast<TMP_Text>().ToArray();
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

            view.HandleNavigate(Vector2.down);
            view.HandleNavigate(Vector2.right);
            Assert.AreEqual(uiText.Get(OptionsModel.TextSpeedKey(TextSpeed.Fast)), values[1].text);

            view.HandlePoint(2);
            view.HandleConfirm();
            Assert.AreEqual(uiText.Get(OptionsModel.OnKey), values[2].text);

            view.HandleCancel();
            Assert.IsTrue(closed);
        }

        [Test]
        public void CreditsView_PagesThroughTheLicensesAndCloses()
        {
            var uiText = UiText();
            host = new GameObject("canvas", typeof(RectTransform));
            host.AddComponent<Canvas>();
            var panel = new GameObject("credits", typeof(RectTransform));
            panel.transform.SetParent(host.transform, false);
            var body = Child<TextMeshProUGUI>("body", panel.transform);
            body.rectTransform.sizeDelta = new Vector2(192f, 60f);
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
