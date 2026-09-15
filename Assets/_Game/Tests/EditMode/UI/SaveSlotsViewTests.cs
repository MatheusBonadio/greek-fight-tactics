using System.Linq;
using Game.Content;
using Game.EditorTools;
using Game.Net;
using Game.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Game.Tests.EditMode.Net.NetSamples;

namespace Game.Tests.EditMode.UI
{
    public sealed class SaveSlotsViewTests
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
        public void SaveMode_ShowsSummariesAndConfirmsBeforeOverwriting()
        {
            var (view, uiText) = CreateView();
            var chosen = -1;
            view.SlotChosen += slot => chosen = slot;

            view.Bind(new SaveSlotsModel(SaveSlotsMode.Save, new[] { SaveSlotSummary.From(0, SampleSave()), SaveSlotSummary.Empty(1), SaveSlotSummary.Empty(2) }));

            Assert.AreEqual(uiText.Get(SaveSlotText.TitleSaveKey), view.Title.text);
            Assert.AreEqual(SaveSlotText.Line(view.Model.Slots[0], uiText), view.Rows[0].text);
            Assert.IsFalse(view.ConfirmPanel.activeSelf);

            view.HandleConfirm();
            Assert.IsTrue(view.ConfirmPanel.activeSelf);
            Assert.AreEqual(uiText.Get(SaveSlotText.ConfirmOverwriteKey), view.ConfirmLabel.text);
            Assert.AreEqual(uiText.Get(SaveSlotText.NoKey), view.ConfirmRows[SaveSlotsModel.ConfirmNo].text);
            Assert.AreEqual(-1, chosen);

            view.HandlePoint(SaveSlotsModel.ConfirmYes);
            view.HandleConfirm();

            Assert.AreEqual(0, chosen);
            Assert.IsFalse(view.ConfirmPanel.activeSelf);
        }

        [Test]
        public void LoadMode_DimsEmptySlotsAndCancelFirstLeavesTheConfirmation()
        {
            var (view, uiText) = CreateView();
            var closed = false;
            view.Closed += () => closed = true;

            view.Bind(new SaveSlotsModel(SaveSlotsMode.Load, new[] { SaveSlotSummary.Empty(0), SaveSlotSummary.From(1, SampleSave()), SaveSlotSummary.Empty(2) }));

            Assert.AreEqual(uiText.Get(SaveSlotText.TitleLoadKey), view.Title.text);
            Assert.AreEqual(view.UnavailableColor, view.Rows[0].color);

            view.HandleCancel();
            Assert.IsTrue(closed);
        }

        private (SaveSlotsView View, UiTextSO Text) CreateView()
        {
            var uiText = AssetDatabase.LoadAssetAtPath<UiTextSO>(DataTools.UiTextPath);
            Assert.IsNotNull(uiText);

            host = new GameObject("slots", typeof(RectTransform));
            var rows = Enumerable.Range(0, 3).Select(index => Child<TextMeshProUGUI>($"row{index}")).Cast<TMP_Text>().ToArray();
            var confirm = new GameObject("confirm", typeof(RectTransform));
            confirm.transform.SetParent(host.transform, false);
            var confirmRows = Enumerable.Range(0, 2).Select(index => Child<TextMeshProUGUI>($"confirm{index}")).Cast<TMP_Text>().ToArray();

            var view = host.AddComponent<SaveSlotsView>();
            view.Configure(Child<TextMeshProUGUI>("title"), rows, Child<Image>("cursor").rectTransform, confirm, Child<TextMeshProUGUI>("question"), confirmRows, Child<Image>("confirmCursor").rectTransform, uiText);
            return (view, uiText);
        }

        private T Child<T>(string name) where T : Component
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(host.transform, false);
            return child.AddComponent<T>();
        }
    }
}
