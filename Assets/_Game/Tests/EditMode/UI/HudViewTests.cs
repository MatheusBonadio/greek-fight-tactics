using System.Linq;
using Game.Battle;
using Game.Content;
using Game.Core;
using Game.EditorTools;
using Game.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests.EditMode.UI
{
    public sealed class HudViewTests
    {
        private const int Width = 64;
        private const float Tolerance = 0.0001f;

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
        public void BarView_FillMovesInWholePixelsWhileDraining()
        {
            var (view, fill, _) = CreateBar();
            view.Bind(new DrainingBar(50, 50));

            view.Bar.SetValue(25);
            view.Advance(DrainingBar.DrainSeconds / 3f);

            Assert.AreEqual(0f, fill.fillAmount * Width % 1f, Tolerance);
            Assert.That(fill.fillAmount, Is.InRange(0.5f, 1f));

            view.Advance(DrainingBar.DrainSeconds);
            Assert.AreEqual(0.5f, fill.fillAmount, Tolerance);
        }

        [Test]
        public void BarView_FlashesForExactlyOneFrameOnDamage()
        {
            var (view, _, flash) = CreateBar();
            view.Bind(new DrainingBar(50, 50));

            view.Bar.SetValue(40);
            view.Advance(0.016f);
            Assert.IsTrue(flash.enabled);

            view.Advance(0.016f);
            Assert.IsFalse(flash.enabled);
        }

        [Test]
        public void BarView_BlinksTheWarningTintOnlyBelowAQuarter()
        {
            var (view, _, _) = CreateBar();

            view.Bind(new DrainingBar(100, 20));
            Assert.IsTrue(view.CriticalTintShown);
            view.Advance(BarView.CriticalBlinkSeconds);
            Assert.IsFalse(view.CriticalTintShown);
            view.Advance(BarView.CriticalBlinkSeconds);
            Assert.IsTrue(view.CriticalTintShown);

            view.Bind(new DrainingBar(100, 80));
            Assert.IsFalse(view.CriticalTintShown);
        }

        [Test]
        public void AudienceMeterView_PullsTheMarkerTowardWhoeverConvinces()
        {
            host = new GameObject("audience", typeof(RectTransform));
            var fill = Child<Image>("fill");
            var marker = Child<Image>("marker").rectTransform;
            var view = host.AddComponent<AudienceMeterView>();
            view.Configure(fill, marker, Width);

            view.Bind(new AudienceMeter(50));
            Assert.AreEqual(0f, marker.anchoredPosition.x, Tolerance);

            view.Meter.SetPlayerShare(75);
            view.Advance(DrainingBar.DrainSeconds);
            Assert.AreEqual(0.75f, fill.fillAmount, Tolerance);
            Assert.AreEqual(16f, marker.anchoredPosition.x, Tolerance);

            view.Meter.SetPlayerShare(0);
            view.Advance(DrainingBar.DrainSeconds);
            Assert.AreEqual(-32f, marker.anchoredPosition.x, Tolerance);
        }

        [Test]
        public void AudienceMeterView_ShowsEachSideShareAsAPercentage()
        {
            host = new GameObject("audience", typeof(RectTransform));
            var view = host.AddComponent<AudienceMeterView>();
            view.Configure(Child<Image>("fill"), Child<Image>("marker").rectTransform, Width);
            view.ConfigureShares(Child<TextMeshProUGUI>("player"), Child<TextMeshProUGUI>("opponent"));

            view.Bind(new AudienceMeter(40));
            Assert.AreEqual("40%", view.PlayerShareLabel.text);
            Assert.AreEqual("60%", view.OpponentShareLabel.text);

            view.Meter.SetPlayerShare(75);
            view.Advance(DrainingBar.DrainSeconds);
            Assert.AreEqual("75%", view.PlayerShareLabel.text);
            Assert.AreEqual("25%", view.OpponentShareLabel.text);
        }

        [Test]
        public void DebaterPanelView_NamesTheDebaterShowsThePortraitAndCountsCoherenceDown()
        {
            host = new GameObject("panel", typeof(RectTransform));
            var (bar, _, _) = CreateBarOn(Child<RectTransform>("bar").gameObject);
            var portrait = Child<Image>("portrait");
            var view = host.AddComponent<DebaterPanelView>();
            view.Configure(Child<TextMeshProUGUI>("name"), portrait, bar, Child<TextMeshProUGUI>("value"));
            var texture = new Texture2D(24, 32);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 24, 32), new Vector2(0.5f, 0.0625f), 16f);

            view.Show(new DebaterBadge("Thrasybios", sprite));
            bar.Bind(new DrainingBar(40, 40));
            view.RenderValue();

            Assert.AreEqual("Thrasybios", view.NameLabel.text);
            Assert.AreSame(sprite, portrait.sprite);
            Assert.IsTrue(portrait.enabled);
            Assert.AreEqual(new Vector2(24f, 32f), portrait.rectTransform.sizeDelta);
            Assert.AreEqual("40/40", view.ValueLabel.text);

            bar.Bar.SetValue(28);
            bar.Advance(DrainingBar.DrainSeconds / 2f);
            view.RenderValue();
            Assert.AreEqual("34/40", view.ValueLabel.text);

            bar.Advance(DrainingBar.DrainSeconds);
            view.RenderValue();
            Assert.AreEqual("28/40", view.ValueLabel.text);

            view.Show(new DebaterBadge("Kleia", null));
            Assert.IsFalse(portrait.enabled);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void ActionMenuView_OutsideThePlayersTurn_ExplainsNothing()
        {
            var (view, _) = CreateMenu();
            view.Bind(Controller(new Argument("Um grão de areia não é um monte, e somar um grão nunca faz o monte.", null, new[] { "um grão", "outro grão" }, 1, ArgumentTones.Curious), Act.Two));
            view.HandlePoint(BattleMenuLayer.Actions, 1);
            Assert.IsNotEmpty(view.ReasonLabel.text);

            view.SetInteractive(false);

            Assert.IsEmpty(view.ReasonLabel.text, "while the opponent thinks, the help box does not nag that it is not her turn");

            view.SetInteractive(true);

            Assert.IsNotEmpty(view.ReasonLabel.text);
        }

        [Test]
        public void ActionMenuView_DimsUnavailableActionsAndShowsTheReasonFromData()
        {
            var (view, uiText) = CreateMenu();
            view.Bind(Controller(new Argument("Um grão de areia não é um monte, e somar um grão nunca faz o monte.", null, new[] { "um grão", "outro grão" }, 1, ArgumentTones.Curious), Act.Two));

            view.HandlePoint(BattleMenuLayer.Actions, 1);

            Assert.AreEqual(uiText.Get(UiTextKeys.For(ActionId.Circulate)), view.ActionRows[0].text);
            Assert.AreEqual(view.UnavailableColor, view.ActionRows[1].color);
            Assert.AreEqual(uiText.Get(UiTextKeys.For(RejectionReason.ArgumentNotRefutable)), view.ReasonLabel.text);

            view.HandleConfirm();
            Assert.IsFalse(view.SubmenuPanel.activeSelf);
        }

        [Test]
        public void ActionMenuView_NavigatesByKeysAndChoosesAFallacyByClick()
        {
            var (view, uiText) = CreateMenu();
            var controller = Controller(new Argument("Quem nasceu no Cerâmico não tem autoridade para falar de justiça na ágora.", "ad_hominem", new[] { "a", "b" }, 1, ArgumentTones.Mocking), Act.One);
            view.Bind(controller);
            BattleMenuChoice? chosen = null;
            view.ChoiceMade += choice => chosen = choice;

            view.HandleNavigate(Vector2.down);
            Assert.AreEqual(ActionId.Refute, controller.Actions.Selected.Action);
            Assert.AreEqual(view.ActionRows[1].rectTransform.anchoredPosition.y + ActionMenuView.CursorOffsetY, view.ActionCursor.anchoredPosition.y, Tolerance);

            view.HandleConfirm();
            Assert.IsTrue(view.SubmenuPanel.activeSelf);
            Assert.AreEqual(uiText.Get(UiTextKeys.ChooseFallacy), view.SubmenuTitle.text);
            Assert.AreEqual("Ad hominem", view.SubmenuRows[0].text);

            view.HandleClick(BattleMenuLayer.Fallacies, 1);

            Assert.IsTrue(chosen.HasValue);
            Assert.AreEqual(ActionId.Refute, chosen.Value.Action);
            Assert.AreEqual(controller.Actions.FallacyOptions(RefutableIds()).ElementAt(1), chosen.Value.FallacyId);
            Assert.IsFalse(view.SubmenuPanel.activeSelf);
        }

        [Test]
        public void ActionMenuView_CursorBlinksEveryHalfSecondAndKeysRestartIt()
        {
            var (view, _) = CreateMenu();
            view.Bind(Controller(new Argument("Quem nasceu no Cerâmico não tem autoridade para falar de justiça na ágora.", "ad_hominem", new[] { "a", "b" }, 1, ArgumentTones.Mocking), Act.One));

            Assert.IsTrue(view.ActionCursor.gameObject.activeSelf);
            view.Advance(ActionMenuView.CursorBlinkSeconds);
            Assert.IsFalse(view.ActionCursor.gameObject.activeSelf);

            view.HandleNavigate(Vector2.down);
            Assert.IsTrue(view.ActionCursor.gameObject.activeSelf);
        }

        [TestCase(0, 15, 5, 0)]
        [TestCase(7, 15, 5, 3)]
        [TestCase(14, 15, 5, 10)]
        [TestCase(2, 3, 5, 0)]
        public void ActionMenuView_SubmenuWindowKeepsTheCursorVisible(int cursor, int count, int visible, int start)
        {
            Assert.AreEqual(start, ActionMenuView.WindowStart(cursor, count, visible));
        }

        private (BarView View, Image Fill, Image Flash) CreateBar()
        {
            host = new GameObject("bar", typeof(RectTransform));
            return CreateBarOn(host);
        }

        private (BarView View, Image Fill, Image Flash) CreateBarOn(GameObject target)
        {
            var fill = Child<Image>("fill", target.transform);
            var flash = Child<Image>("flash", target.transform);
            var view = target.AddComponent<BarView>();
            view.Configure(fill, flash, Width);
            return (view, fill, flash);
        }

        private (ActionMenuView View, UiTextSO Text) CreateMenu()
        {
            host = new GameObject("menu", typeof(RectTransform));
            var uiText = AssetDatabase.LoadAssetAtPath<UiTextSO>(DataTools.UiTextPath);
            var catalog = AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath);
            Assert.IsNotNull(uiText);
            Assert.IsNotNull(catalog);

            var rows = Enumerable.Range(0, 6).Select(index => Label($"action{index}", new Vector2(18f, -8f - index * 12f))).ToArray();
            var cursor = Child<Image>("cursor").rectTransform;
            var reason = Label("reason", Vector2.zero);
            var panel = new GameObject("submenu", typeof(RectTransform));
            panel.transform.SetParent(host.transform, false);
            var title = Label("title", Vector2.zero);
            var options = Enumerable.Range(0, 5).Select(index => Label($"option{index}", new Vector2(18f, -16f - index * 12f))).ToArray();
            var optionCursor = Child<Image>("optionCursor").rectTransform;

            var view = host.AddComponent<ActionMenuView>();
            view.Configure(rows, cursor, reason, panel, title, options, optionCursor, uiText, catalog);
            return (view, uiText);
        }

        private TMP_Text Label(string name, Vector2 position)
        {
            var label = Child<TextMeshProUGUI>(name);
            label.rectTransform.anchoredPosition = position;
            return label;
        }

        private T Child<T>(string name, Transform parent = null) where T : Component
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent ? parent : host.transform, false);
            return child.TryGetComponent<T>(out var existing) ? existing : child.AddComponent<T>();
        }

        private static string[] RefutableIds() =>
            AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath).RefutableIds.ToArray();

        private static BattleMenuController Controller(Argument argument, Act act)
        {
            var state = BattleArena.CreateDuel(act, Profile.Philosopher, 7, BattleArena.OpponentOpens);
            var resolver = new BattleResolver();
            state.Apply(resolver.Resolve(Play.Argue(BattleArena.OpponentId, BattleArena.PlayerId, argument), state));
            return new BattleMenuController(new ActionMenuModel(state, resolver), RefutableIds());
        }
    }
}
