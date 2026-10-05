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
using UnityEngine.UI;

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
        public void Prefab_DrawsEveryPieceInsideTheBottomStrip()
        {
            var hud = InstantiateHud();
            var strip = (RectTransform)host.transform.Find("Strip");
            Assert.IsNotNull(strip, "the HUD keeps its pieces under a Strip rect");
            Assert.AreEqual(BattleHudTools.StripHeight, strip.rect.height);
            Assert.AreEqual(Mathf.RoundToInt(BattleHudTools.CanvasHeight * HudLayout.BottomStripFraction), BattleHudTools.StripHeight);

            var graphics = host.GetComponentsInChildren<Graphic>(true);
            Assert.IsNotEmpty(graphics);
            var submenu = strip.Find("Submenu");
            Assert.IsNotNull(submenu, "the fallacy grid is the one piece allowed above the strip");
            foreach (var graphic in graphics)
            {
                Assert.IsTrue(graphic.transform.IsChildOf(strip), $"{graphic.name} is outside the strip");
                if (graphic.transform == submenu || graphic.transform.IsChildOf(submenu))
                {
                    continue;
                }

                var (bottom, top) = VerticalSpan(graphic.rectTransform, strip);
                Assert.GreaterOrEqual(bottom, 0f, graphic.name);
                Assert.LessOrEqual(top, BattleHudTools.StripHeight, graphic.name);
            }

            var (grid, gridTop) = VerticalSpan((RectTransform)submenu, strip);
            Assert.GreaterOrEqual(grid, BattleHudTools.StripHeight, "the fallacy grid never covers the HUD it grows out of");
            Assert.LessOrEqual(gridTop, BattleHudTools.CanvasHeight, "the fallacy grid stays on screen");

            Assert.IsTrue(hud.Dialogue.transform.IsChildOf(strip));
            Assert.IsTrue(hud.Menu.transform.IsChildOf(strip));
        }

        [Test]
        public void Prefab_GivesKleiaAndTheChallengerTheirOwnSideColourAndBar()
        {
            var hud = InstantiateHud();
            var player = hud.PlayerPanel;
            var opponent = hud.OpponentPanel;

            Assert.AreEqual(0f, ((RectTransform)player.transform).anchorMin.x, "Kleia sits on the left");
            Assert.AreEqual(1f, ((RectTransform)opponent.transform).anchorMin.x, "the challenger sits on the right");

            var playerPlate = player.NameLabel.GetComponentInParent<UiPlate>().Background.color;
            var opponentPlate = opponent.NameLabel.GetComponentInParent<UiPlate>().Background.color;
            Assert.Greater(playerPlate.b, playerPlate.r, "Kleia's plate is aegean blue");
            Assert.Greater(opponentPlate.r, opponentPlate.b, "the challenger's plate is red");
            Assert.AreEqual(playerPlate, ColorOf(player.transform, "FaceFrame"));
            Assert.AreEqual(opponentPlate, ColorOf(opponent.transform, "FaceFrame"));

            var playerFill = FillOf(player.Coherence);
            var opponentFill = FillOf(opponent.Coherence);
            Assert.AreEqual(BattleHudTools.PlayerBarSprite, playerFill.sprite.name);
            Assert.AreEqual(BattleHudTools.ChallengerBarSprite, opponentFill.sprite.name);
            Assert.AreEqual((int)Image.OriginHorizontal.Left, playerFill.fillOrigin);
            Assert.AreEqual((int)Image.OriginHorizontal.Right, opponentFill.fillOrigin);

            var audienceFill = hud.Audience.GetComponentsInChildren<Image>(true).First(image => image.type == Image.Type.Filled);
            Assert.AreEqual(BattleHudTools.PlayerBarSprite, audienceFill.sprite.name, "Kleia's share of the crowd fills in her colour");
            Assert.AreEqual(BattleHudTools.ChallengerBarSprite, audienceFill.transform.parent.GetComponent<Image>().sprite.name, "the rest of the crowd shows the challenger's colour");

            var uiText = AssetDatabase.LoadAssetAtPath<UiTextSO>(DataTools.UiTextPath);
            var captions = host.GetComponentsInChildren<UiTextLabel>(true).Select(label => label.Key).ToList();
            Assert.AreEqual(2, captions.Count(key => key == UiTextKeys.CoherenceCaption));
            Assert.AreEqual(1, captions.Count(key => key == UiTextKeys.AudienceCaption));
            Assert.AreEqual(uiText.Get(UiTextKeys.CoherenceCaption), player.transform.Find("Caption").GetComponent<TMPro.TMP_Text>().text);
        }

        [Test]
        public void Introduce_LabelsEachPanelWithItsDebaterAndValue()
        {
            var hud = InstantiateHud();
            var state = BattleArena.CreateDuel(Act.One, Profile.Philosopher, 7, BattleArena.OpponentOpens);
            var catalog = AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath);
            hud.Bind(state, new BattleMenuController(new ActionMenuModel(state, new BattleResolver()), catalog.RefutableIds));

            hud.Introduce(new DebaterBadge("Kleia", null), new DebaterBadge("Thrasybios", null));

            Assert.AreEqual("Kleia", hud.PlayerPanel.NameLabel.text);
            Assert.AreEqual("Thrasybios", hud.OpponentPanel.NameLabel.text);
            Assert.AreEqual(TMPro.FontStyles.UpperCase, hud.OpponentPanel.NameLabel.fontStyle & TMPro.FontStyles.UpperCase);
            Assert.AreEqual(DebaterPanelView.ValueText(state.Player.Coherence, state.Player.MaxCoherence), hud.PlayerPanel.ValueLabel.text);
            Assert.AreEqual(DebaterPanelView.ValueText(state.Opponent.Coherence, state.Opponent.MaxCoherence), hud.OpponentPanel.ValueLabel.text);
            Assert.AreEqual(AudienceMeterView.ShareText(state.PlayerAudience), hud.Audience.PlayerShareLabel.text);
            Assert.IsNotNull(hud.PlayerPanel.MoraleLabel, "each panel shows how the crowd weighs on its debater");
            Assert.IsNotNull(hud.OpponentPanel.MoraleLabel);
            Assert.AreEqual(BattleHudTools.MoraleFavourColor, hud.PlayerPanel.FavourColor);
            Assert.AreEqual(BattleHudTools.MoraleAgainstColor, hud.PlayerPanel.AgainstColor);
            var shift = MoraleRule.ShiftFor(state, state.Player);
            Assert.AreEqual(shift == 0 ? string.Empty : hud.PlayerPanel.MoraleText(shift), hud.PlayerPanel.MoraleLabel.text);
        }

        [Test]
        public void OpeningASubmenu_LeavesTheDialogueWhereItIsAndKeepsTheGridClearOfIt()
        {
            var hud = InstantiateHud();
            var catalog = AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath);
            var resolver = new BattleResolver();
            var state = BattleArena.CreateDuel(Act.One, Profile.Philosopher, 7, BattleArena.OpponentOpens);
            state.Apply(resolver.Resolve(Play.Argue(BattleArena.OpponentId, BattleArena.PlayerId, new Argument("Quem nasceu no Cerâmico não tem autoridade para falar de justiça na ágora.", "ad_hominem", new[] { "a", "b" }, 1, ArgumentTones.Mocking)), state));
            hud.Bind(state, new BattleMenuController(new ActionMenuModel(state, resolver), catalog.RefutableIds));
            Assert.AreEqual(hud.DialogueLeft, hud.DialogueArea.offsetMin.x);

            hud.Menu.HandleNavigate(Vector2.down);
            hud.Menu.HandleConfirm();
            Assert.IsTrue(hud.Menu.SubmenuPanel.activeSelf);
            Assert.AreEqual(hud.DialogueLeft, hud.DialogueArea.offsetMin.x, "the grid grows upward, so the dialogue stays put");
            var submenu = (RectTransform)hud.Menu.SubmenuPanel.transform;
            Assert.GreaterOrEqual(submenu.anchoredPosition.y, BattleHudTools.StripHeight, "the grid never covers the dialogue it talks about");
            Assert.LessOrEqual(submenu.anchoredPosition.x + submenu.sizeDelta.x, BattleHudTools.CanvasWidth, "the grid stays on screen");

            hud.Menu.HandleCancel();
            Assert.IsFalse(hud.Menu.SubmenuPanel.activeSelf);
            Assert.AreEqual(hud.DialogueLeft, hud.DialogueArea.offsetMin.x);
        }

        [Test]
        public void FallacyList_FitsTheWholeRefutableCatalogueWithoutScrolling()
        {
            var hud = InstantiateHud();
            var catalog = AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath);
            var resolver = new BattleResolver();
            var state = BattleArena.CreateDuel(Act.One, Profile.Philosopher, 7, BattleArena.OpponentOpens);
            state.Apply(resolver.Resolve(Play.Argue(BattleArena.OpponentId, BattleArena.PlayerId, new Argument("Quem nasceu no Cerâmico não tem autoridade para falar de justiça na ágora.", "ad_hominem", new[] { "a", "b" }, 1, ArgumentTones.Mocking)), state));
            var controller = new BattleMenuController(new ActionMenuModel(state, resolver), catalog.RefutableIds);
            hud.Bind(state, controller);

            hud.Menu.HandleNavigate(Vector2.down);
            hud.Menu.HandleConfirm();
            Assert.AreEqual(catalog.RefutableIds.Count(), controller.SubmenuOptions.Count, "every refutable fallacy is offered");
            Assert.LessOrEqual(controller.SubmenuOptions.Count, hud.Menu.SubmenuRows.Count, "the grid holds the whole catalogue");
            Assert.AreEqual(BattleHudTools.SubmenuCells, hud.Menu.SubmenuRows.Count);
            Assert.IsFalse(hud.Menu.MoreAbove.activeSelf, "nothing hides above, so no arrow");
            Assert.IsFalse(hud.Menu.MoreBelow.activeSelf, "nothing hides below, so no arrow");

            for (var step = 1; step < controller.SubmenuOptions.Count; step++)
            {
                hud.Menu.HandleNavigate(Vector2.down);
            }

            Assert.AreEqual(controller.SubmenuOptions.Count - 1, controller.SubmenuCursor, "the cursor must stop on the last option");
            Assert.IsFalse(hud.Menu.MoreAbove.activeSelf, "the player never scrolls a grid that already fits");
            Assert.IsFalse(hud.Menu.MoreBelow.activeSelf);
        }

        [Test]
        public void OutsideThePlayersTurn_TheMenuHidesItsCursorAndIgnoresInput()
        {
            var hud = InstantiateHud();
            var catalog = AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath);
            var resolver = new BattleResolver();
            var state = BattleArena.CreateDuel(Act.One, Profile.Philosopher, 7, BattleArena.OpponentOpens);
            var controller = new BattleMenuController(new ActionMenuModel(state, resolver), catalog.RefutableIds);
            hud.Bind(state, controller);
            BattleMenuChoice? chosen = null;
            hud.Menu.ChoiceMade += choice => chosen = choice;

            hud.Menu.SetInteractive(false);
            hud.Menu.HandleNavigate(Vector2.down);
            hud.Menu.HandleConfirm();
            hud.Menu.HandleClick(BattleMenuLayer.Actions, 0);

            Assert.IsFalse(hud.Menu.ActionCursor.gameObject.activeSelf, "no option cursor during the opponent's turn");
            Assert.AreEqual(0, controller.Actions.CursorIndex);
            Assert.IsFalse(chosen.HasValue);

            hud.Menu.SetInteractive(true);
            Assert.IsTrue(hud.Menu.ActionCursor.gameObject.activeSelf);
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
            state.Apply(resolver.Resolve(Play.Argue(BattleArena.OpponentId, BattleArena.PlayerId, new Argument("Quem nasceu no Cerâmico não tem autoridade para falar de justiça na ágora.", "ad_hominem", new[] { "a", "b" }, Argument.NoWeakPremise, ArgumentTones.Mocking)), state));
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

        private static Image FillOf(BarView bar) =>
            bar.GetComponentsInChildren<Image>(true).First(image => image.type == Image.Type.Filled);

        private static Color ColorOf(Transform root, string name) =>
            root.GetComponentsInChildren<Image>(true).First(image => image.name == name).color;

        private static (float Bottom, float Top) VerticalSpan(RectTransform rect, RectTransform space)
        {
            var corners = new Vector3[4];
            rect.GetLocalCorners(corners);
            var heights = corners.Select(corner => InSpace(rect, corner, space).y - space.rect.yMin).ToList();
            return (heights.Min(), heights.Max());
        }

        private static Vector3 InSpace(Transform transform, Vector3 point, Transform space)
        {
            for (var current = transform; current != space; current = current.parent)
            {
                point = current.localRotation * Vector3.Scale(point, current.localScale) + current.localPosition;
            }

            return point;
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
