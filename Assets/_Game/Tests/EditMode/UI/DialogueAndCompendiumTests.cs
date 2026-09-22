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
    public sealed class DialogueAndCompendiumTests
    {
        private const string LongSpeech =
            "Você fala da justiça como quem fala do barro: sabe moldar, mas não sabe dizer o que é. Se a justiça é dar a cada um o que lhe é devido, então devemos devolver a espada ao amigo que enlouqueceu? Responda devagar, porque a ágora inteira está ouvindo e ninguém aqui esquece uma resposta apressada.";

        private const int BodyWidth = 192;
        private const int BodyHeight = 3 * BattleHudTools.LineHeight;
        private const float WidthTolerance = 0.01f;

        private GameObject host;
        private Texture2D texture;

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                Object.DestroyImmediate(host);
            }

            if (texture != null)
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void DialogueBox_WrapsEveryLineInsideTheBoxWidth()
        {
            var view = CreateDialogue();

            view.Show(new DialogueLine("Menexeno", null, LongSpeech));

            var info = view.Body.textInfo;
            Assert.Greater(info.lineCount, 1);
            for (var line = 0; line < info.lineCount; line++)
            {
                Assert.LessOrEqual(info.lineInfo[line].width, BodyWidth + WidthTolerance);
            }
        }

        [Test]
        public void DialogueBox_TypesAtTheConfiguredSpeedSkipsAndPagesUntilFinished()
        {
            var view = CreateDialogue();
            var finished = false;
            view.Finished += () => finished = true;
            view.CharactersPerSecond = 10f;

            view.Show(new DialogueLine("Menexeno", null, LongSpeech));
            Assert.Greater(view.PageCount, 1);
            Assert.AreEqual(0, view.Body.maxVisibleCharacters);

            view.Advance(0.35f);
            Assert.AreEqual(3, view.Body.maxVisibleCharacters);

            view.Confirm();
            Assert.IsTrue(view.IsPageComplete);
            Assert.AreEqual(1, view.Page);

            view.Confirm();
            Assert.AreEqual(2, view.Page);
            Assert.IsFalse(view.IsPageComplete);

            for (var step = 0; step < 4 * view.PageCount && view.IsOpen; step++)
            {
                view.Confirm();
            }

            Assert.IsTrue(finished);
            Assert.IsFalse(view.IsOpen);
        }

        [Test]
        public void DialogueBox_ShowsTheSpeakerNameAndHidesTheFrameWithoutAPortrait()
        {
            var view = CreateDialogue();
            texture = new Texture2D(32, 32);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 16f);

            view.Show(new DialogueLine("Damaris", sprite, "Eu ouvi tudo, e não concordo com nada."));
            Assert.AreEqual("Damaris", view.SpeakerLabel.text);
            Assert.AreEqual(sprite, view.Portrait.sprite);
            Assert.IsTrue(view.PortraitFrame.activeSelf);

            view.Show(new DialogueLine("Plateia", null, "Risos na ágora."));
            Assert.IsFalse(view.PortraitFrame.activeSelf);
            Object.DestroyImmediate(sprite);
        }

        [Test]
        public void DialogueBox_KeepsTheTextClearOfTheSpeakerAndOfTheChoices()
        {
            var view = CreateDialogue();
            view.ConfigureMargins(46f, 134f);
            texture = new Texture2D(32, 32);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 16f);

            view.Show(new DialogueLine("Damaris", sprite, "Eu ouvi tudo."));
            Assert.AreEqual(new Vector4(46f, 0f, 0f, 0f), view.Body.margin);

            view.Show(new DialogueLine("Thrasybios", null, "Aceita as aulas?"), new[] { "Aceitar", "Recusar" });
            Assert.AreEqual(new Vector4(46f, 0f, 134f, 0f), view.Body.margin, "a named speaker keeps the column under the portrait free for the name");

            view.Show(new DialogueLine(string.Empty, null, "A roda se desfaz."));
            Assert.AreEqual(Vector4.zero, view.Body.margin);
            Object.DestroyImmediate(sprite);
        }

        [Test]
        public void DialogueBox_OffersTheOptionsOnlyAfterTheQuestionIsReadAndReportsThePickedOne()
        {
            var view = CreateDialogue();
            var panel = Child<Image>("choices").rectTransform;
            var rows = Enumerable.Range(0, DialogueChoice.MaxOptions).Select(index => Child<TextMeshProUGUI>($"choice{index}", panel)).Cast<TMP_Text>().ToArray();
            view.ConfigureChoices(panel, rows, Child<Image>("cursor", panel).rectTransform);
            var picked = -1;
            var finished = false;
            view.ChoiceMade += index => picked = index;
            view.Finished += () => finished = true;
            view.CharactersPerSecond = 10f;

            view.Show(new DialogueLine("Thrasybios", null, "Aceita as aulas?"), new[] { "Aceitar as aulas", "Recusar" });
            Assert.IsTrue(view.HasChoices);
            Assert.IsFalse(view.IsAwaitingChoice, "the options wait until the question is typed");
            Assert.IsFalse(panel.gameObject.activeSelf);
            view.HandleNavigate(Vector2.down);
            view.HandleConfirm();
            Assert.AreEqual(0, view.ChoiceCursor, "navigating while the text types does nothing");
            Assert.IsTrue(view.IsOpen, "confirming from the menu input cannot skip the question");

            view.Confirm();
            Assert.IsTrue(view.IsAwaitingChoice);
            Assert.IsTrue(panel.gameObject.activeSelf);
            Assert.AreEqual("Aceitar as aulas", rows[0].text);
            Assert.AreEqual("Recusar", rows[1].text);
            Assert.IsFalse(rows[2].gameObject.activeSelf);
            Assert.IsFalse(rows[3].gameObject.activeSelf);

            view.HandleNavigate(Vector2.down);
            Assert.AreEqual(1, view.ChoiceCursor);
            view.HandleNavigate(Vector2.down);
            Assert.AreEqual(0, view.ChoiceCursor, "the cursor wraps around the options");
            view.HandlePoint(1);
            Assert.AreEqual(1, view.ChoiceCursor);
            view.HandlePoint(3);
            Assert.AreEqual(1, view.ChoiceCursor, "pointing at an unused row keeps the cursor");
            view.HandleCancel();
            Assert.IsTrue(view.IsAwaitingChoice, "cancel cannot dodge the question");

            view.HandleConfirm();
            Assert.AreEqual(1, picked);
            Assert.IsFalse(finished, "answering is not a plain finish");
            Assert.IsFalse(view.IsOpen);
            Assert.IsFalse(view.HasChoices);

            view.Show(new DialogueLine(string.Empty, null, "A roda se desfaz."));
            Assert.IsFalse(view.HasChoices);
            Assert.IsFalse(panel.gameObject.activeSelf);
            view.Confirm();
            view.Confirm();
            Assert.IsTrue(finished);
        }

        [TestCase("á é í ó ú â ê ô ã õ à ç Ç Ã Õ")]
        [TestCase("\"retas\" “curvas” ‘simples’ — travessão… reticências...")]
        public void DefaultFont_CoversPortugueseAccentsQuotesDashAndEllipsis(string sample)
        {
            var font = TMP_Settings.defaultFontAsset;
            Assert.IsNotNull(font);

            var covered = font.HasCharacters(sample, out uint[] missing, true, true);

            Assert.IsTrue(covered, $"missing glyphs: {string.Join(", ", (missing ?? new uint[0]).Select(code => $"U+{code:X4}"))}");
        }

        [Test]
        public void MissingGlyph_IsReplacedByAVisibleSquareInsteadOfDisappearing()
        {
            var font = TMP_Settings.defaultFontAsset;
            var replacement = TMP_Settings.missingGlyphCharacter == 0 ? '□' : (char)TMP_Settings.missingGlyphCharacter;
            Assert.IsFalse(TMP_Settings.warningsDisabled);
            Assert.IsTrue(font.HasCharacter(replacement, true, true), "the replacement glyph must exist so a missing character stays visible");

            host = new GameObject("canvas", typeof(RectTransform));
            host.AddComponent<Canvas>();
            var label = Child<TextMeshProUGUI>("label");
            label.font = font;
            label.text = "a漢b";
            label.ForceMeshUpdate();

            Assert.AreEqual(3, label.textInfo.characterCount);
            Assert.AreEqual(replacement, label.textInfo.characterInfo[1].character);
            Assert.IsTrue(label.textInfo.characterInfo[1].isVisible);
        }

        [Test]
        public void FallacyReveal_FloatsTheIconAboveTheActorHead()
        {
            host = new GameObject("actor");
            var actorRenderer = host.AddComponent<SpriteRenderer>();
            actorRenderer.sortingOrder = 5;
            var actor = host.AddComponent<ActorView>();
            actor.Configure(Vector2Int.zero, Game.Grid.Facing.SouthEast, actorRenderer, null);
            var catalog = Catalog();

            var reveal = FallacyRevealView.Attach(actor);

            Assert.AreEqual(FallacyRevealView.IconOffsetUnits, reveal.transform.localPosition.y, 0.0001f);
            Assert.AreEqual(5 + FallacyRevealView.SortingOrderAboveActor, reveal.IconRenderer.sortingOrder);
            Assert.IsFalse(reveal.IsRevealed);

            Assert.IsTrue(catalog.TryGet("ad_hominem", out var fallacy));
            reveal.Reveal(fallacy);
            Assert.IsTrue(reveal.IsRevealed);
            Assert.AreEqual(fallacy.Icon, reveal.IconRenderer.sprite);
            Assert.AreEqual(16f, fallacy.Icon.rect.width);
            Assert.AreEqual("ad_hominem", reveal.RevealedFallacyId);

            reveal.Hide();
            Assert.IsFalse(reveal.IsRevealed);
        }

        [Test]
        public void Compendium_ListsUnlockedFallaciesWithDescriptionHintAndIcon()
        {
            var catalog = Catalog();
            var uiText = AssetDatabase.LoadAssetAtPath<UiTextSO>(DataTools.UiTextPath);
            var view = CreateCompendium(catalog, uiText);
            var compendium = new FallacyCompendium(catalog.All.Select(fallacy => fallacy.Id));

            view.Bind(compendium);
            Assert.AreEqual(uiText.Get(CompendiumView.EmptyKey), view.Title.text);
            Assert.AreEqual(string.Format(uiText.Get(CompendiumView.CounterKey), 0, catalog.Count), view.Counter.text);
            Assert.IsFalse(view.Icon.enabled);

            compendium.Unlock("straw_man");
            compendium.Unlock("ad_hominem");
            view.Render();

            var first = compendium.UnlockedIds[0];
            Assert.IsTrue(catalog.TryGet(compendium.UnlockedIds[1], out var second));
            view.HandleNavigate(Vector2.down);

            Assert.AreEqual(second.DisplayName, view.Title.text);
            Assert.AreEqual(second.Description, view.Description.text);
            Assert.AreEqual(second.PlayerHint, view.Hint.text);
            Assert.AreEqual(second.Icon, view.Icon.sprite);
            Assert.IsTrue(catalog.TryGet(first, out var firstFallacy));
            Assert.AreEqual(firstFallacy.DisplayName, view.Rows[0].text);
            Assert.IsFalse(view.Rows[2].gameObject.activeSelf);
            StringAssert.StartsWith($"2/{catalog.Count}", view.Counter.text);
        }

        [Test]
        public void AssistedMode_HighlightsTheWeakPremiseInTheMaieuticsSubmenu()
        {
            var catalog = Catalog();
            var uiText = AssetDatabase.LoadAssetAtPath<UiTextSO>(DataTools.UiTextPath);
            host = new GameObject("menu", typeof(RectTransform));
            var rows = Enumerable.Range(0, 6).Select(index => Child<TextMeshProUGUI>($"action{index}")).Cast<TMP_Text>().ToArray();
            var options = Enumerable.Range(0, 5).Select(index => Child<TextMeshProUGUI>($"option{index}")).Cast<TMP_Text>().ToArray();
            var panel = new GameObject("panel", typeof(RectTransform));
            panel.transform.SetParent(host.transform, false);
            var view = host.AddComponent<ActionMenuView>();
            view.Configure(rows, null, Child<TextMeshProUGUI>("reason"), panel, Child<TextMeshProUGUI>("title"), options, null, uiText, catalog);

            var resolver = new BattleResolver();
            var state = BattleArena.CreateDuel(Act.One, Profile.Philosopher, 7, BattleArena.OpponentOpens);
            state.Apply(resolver.Resolve(Play.Argue(BattleArena.OpponentId, BattleArena.PlayerId, new Argument("Quem nasceu no Cerâmico não tem autoridade para falar de justiça na ágora.", "ad_hominem", new[] { "Kleia é do Cerâmico", "Oleiros não entendem de justiça" }, 1, ArgumentTones.Mocking)), state));
            view.Bind(new BattleMenuController(new ActionMenuModel(state, resolver), catalog.RefutableIds));

            view.HandlePoint(BattleMenuLayer.Actions, 2);
            view.HandleConfirm();
            Assert.AreEqual(view.AvailableColor, options[1].color);

            view.AssistedMode = true;
            Assert.AreEqual(view.HintColor, options[1].color);
            Assert.AreEqual(view.AvailableColor, options[0].color);
        }

        private DialogueBoxView CreateDialogue()
        {
            host = new GameObject("dialogue", typeof(RectTransform));
            host.AddComponent<Canvas>();
            var box = new GameObject("box", typeof(RectTransform));
            box.transform.SetParent(host.transform, false);
            var body = Child<TextMeshProUGUI>("body", box.transform);
            body.rectTransform.sizeDelta = new Vector2(BodyWidth, BodyHeight);
            body.font = TMP_Settings.defaultFontAsset;
            body.fontSize = BattleHudTools.FontSize;
            body.textWrappingMode = TextWrappingModes.Normal;
            var frame = new GameObject("frame", typeof(RectTransform));
            frame.transform.SetParent(host.transform, false);
            var portrait = frame.AddComponent<Image>();

            var view = host.AddComponent<DialogueBoxView>();
            view.Configure(box, body, Child<TextMeshProUGUI>("speaker"), portrait, frame);
            return view;
        }

        private CompendiumView CreateCompendium(FallacyCatalogSO catalog, UiTextSO uiText)
        {
            host = new GameObject("compendium", typeof(RectTransform));
            var rows = Enumerable.Range(0, 4).Select(index => Child<TextMeshProUGUI>($"row{index}")).Cast<TMP_Text>().ToArray();
            var view = host.AddComponent<CompendiumView>();
            view.Configure(
                Child<TextMeshProUGUI>("header"),
                rows,
                Child<Image>("cursor").rectTransform,
                Child<TextMeshProUGUI>("title"),
                Child<TextMeshProUGUI>("description"),
                Child<TextMeshProUGUI>("hint"),
                Child<TextMeshProUGUI>("counter"),
                Child<Image>("icon"),
                catalog,
                uiText);
            return view;
        }

        private T Child<T>(string name, Transform parent = null) where T : Component
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent ? parent : host.transform, false);
            return child.AddComponent<T>();
        }

        private static FallacyCatalogSO Catalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath);
            Assert.IsNotNull(catalog);
            return catalog;
        }
    }
}
