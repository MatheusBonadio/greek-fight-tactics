using System;
using Game.Content;
using Game.Core;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static Game.EditorTools.UiBuilder;

namespace Game.EditorTools
{
    public static class BattleHudTools
    {
        public const string HudPrefabPath = "Assets/_Game/Prefabs/UI/BattleHud.prefab";
        public const string CompendiumPrefabPath = "Assets/_Game/Prefabs/UI/Compendium.prefab";
        public const string JurorMeterPrefabPath = "Assets/_Game/Prefabs/UI/JurorMeter.prefab";
        public const string ExplorationDialoguePrefabPath = "Assets/_Game/Prefabs/UI/ExplorationDialogue.prefab";
        public const string UiSpriteFolder = ArtTools.PlaceholderFolder + "/ui";
        public const string PlayerBarSprite = "bar_coherence";
        public const string ChallengerBarSprite = "bar_challenger";
        public const string PortraitFrameSprite = "frame_portrait";
        public const int ExplorationDialogueSortingOrder = 5;
        public const int CanvasWidth = UiBuilder.CanvasWidth;
        public const int CanvasHeight = UiBuilder.CanvasHeight;
        public const int FontSize = UiBuilder.FontSize;
        public const int LineHeight = UiBuilder.LineHeight;
        public const int RowHeight = UiBuilder.RowHeight;
        public const int StripHeight = (int)(CanvasHeight * HudLayout.BottomStripFraction);
        public const int Margin = 2;
        public const int Gap = 2;
        public const int MenuWidth = 96;
        public const int MenuHeight = StripHeight - 2 * Margin;
        public const int MenuRows = 6;
        public const int TierHeight = 28;
        public const int MessageHeight = StripHeight - 2 * Margin - Gap - TierHeight;
        public const int MessageLeft = Margin + MenuWidth + Gap;
        public const int SubmenuColumns = 3;
        public const int SubmenuRows = 5;
        public const int SubmenuColumnWidth = 176;
        public const int SubmenuWidth = SubmenuColumns * SubmenuColumnWidth;
        public const int SubmenuCells = SubmenuColumns * SubmenuRows;
        public const int SubmenuTopPadding = 4;
        public const int ScrollArrowInset = 8;
        public const float UpArrowDegrees = 90f;
        public const float DownArrowDegrees = -90f;
        public const int SubmenuTitleWidth = 200;
        public const int BadgeWidth = 186;
        public const int BadgeInset = 2;
        public const int FaceFrameSize = 24;
        public const int FaceSize = 22;
        public const int BadgeContent = BadgeInset + FaceFrameSize + 3;
        public const int ValueWidth = 36;
        public const int MoraleWidth = 48;
        public const int CaptionWidth = 60;
        public const int MeterWidth = 128;
        public const int ShareWidth = 28;
        public const int BarWidth = 64;
        public const int BarHeight = 8;
        public const int PortraitSize = 40;
        public const int PortraitInner = 32;
        public const int DialoguePadding = 6;
        public const int DialogueTopPadding = 6;
        public const int DialogueBottomPadding = 2;
        public const int PortraitColumnWidth = 64;
        public const int SpeakerPlateHeight = RowHeight;
        public const int ExplorationDialogueHeight = StripHeight - 2 * Margin;
        public const int ChoiceBottom = 4;
        public const int CompendiumWidth = 440;
        public const int CompendiumHeight = 184;
        public const int CompendiumRows = 12;
        public const int CompendiumListWidth = 150;
        public const int JurorPadding = 4;
        public static readonly Color MoraleFavourColor = AccentColor;
        public static readonly Color MoraleAgainstColor = ChallengerColor;

        public static void BuildHud() => BatchRunner.Run(BuildHudPrefabs);

        public static void BuildHudPrefabs()
        {
            var uiText = AssetDatabase.LoadAssetAtPath<UiTextSO>(DataTools.UiTextPath)
                ?? throw new InvalidOperationException($"missing {DataTools.UiTextPath}; run DataTools.Regenerate first");
            var catalog = AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath)
                ?? throw new InvalidOperationException($"missing {DataTools.CatalogPath}; run DataTools.Regenerate first");
            var controls = Load<InputActionAsset>(SandboxTools.ControlsPath);

            SavePrefab(HudPrefabPath, root => BuildHudRoot(root, uiText, catalog, controls));
            SavePrefab(CompendiumPrefabPath, root => BuildCompendiumRoot(root, uiText, catalog, controls));
            SavePrefab(JurorMeterPrefabPath, BuildJurorMeterRoot);
            SavePrefab(ExplorationDialoguePrefabPath, BuildExplorationDialogueRoot);
        }

        public static CompendiumView CreateCompendiumView(GameObject host, UiTextSO uiText, FallacyCatalogSO catalog)
        {
            var view = host.AddComponent<CompendiumView>();
            var panel = CreateBox(host.transform, "Panel", Center, Vector2.zero, new Vector2(CompendiumWidth, CompendiumHeight));

            var header = CreateTab(panel.transform, "Header", TopLeft, TabInset, AccentColor, TrackColor);
            header.fontStyle = FontStyles.UpperCase;
            var (rows, cursor) = CreatePanelRows(panel.transform, CompendiumRows, new Vector2(RowInset, -BoxTopPadding - Gap), CompendiumListWidth, view);

            var detailLeft = RowInset + CompendiumListWidth + 12f;
            var detailWidth = CompendiumWidth - detailLeft - 8f;
            var icon = CreateImage(panel.transform, "Icon", null, TopLeft, new Vector2(detailLeft, -BoxTopPadding - Gap), new Vector2(16f, 16f), Color.white);
            var title = CreateLabel(panel.transform, "Title", TopLeft, new Vector2(detailLeft + 20f, -BoxTopPadding - Gap - 1f), new Vector2(detailWidth - 20f, LineHeight), TextAlignmentOptions.Left);
            title.color = AccentColor;
            var description = CreateLabel(panel.transform, "Description", TopLeft, new Vector2(detailLeft, -BoxTopPadding - 24f), new Vector2(detailWidth, 4 * LineHeight), TextAlignmentOptions.TopLeft, true);
            var hint = CreateLabel(panel.transform, "Hint", TopLeft, new Vector2(detailLeft, -BoxTopPadding - 32f - 4 * LineHeight), new Vector2(detailWidth, 4 * LineHeight), TextAlignmentOptions.TopLeft, true);
            hint.color = AccentColor;
            var counter = CreateLabel(panel.transform, "Counter", BottomLeft, new Vector2(RowInset, 4f), new Vector2(CompendiumListWidth, LineHeight), TextAlignmentOptions.Left);
            counter.color = CaptionColor;

            view.Configure(header, rows, cursor, title, description, hint, counter, icon, catalog, uiText);
            return view;
        }

        private static void BuildExplorationDialogueRoot(GameObject root)
        {
            ConfigureCanvas(root).sortingOrder = ExplorationDialogueSortingOrder;
            var area = CreateStretch(root.transform, "Dialogue", BottomLeft, BottomRight, new Vector2(Margin, Margin), new Vector2(-Margin, Margin + ExplorationDialogueHeight));
            CreateDialogue(area);
        }

        private static void BuildJurorMeterRoot(GameObject root)
        {
            ConfigureCanvas(root);
            var frameSize = new Vector2(JurorLayout.Width + 2 * JurorPadding, JurorLayout.Height + 2 * JurorPadding);
            var frame = CreateBox(root.transform, "JurorFrame", BottomCenter, new Vector2(0f, Margin + MessageHeight + Gap), frameSize);
            var meter = CreateRect(frame.transform, "Meter", Center, Vector2.zero, new Vector2(JurorLayout.Width, JurorLayout.Height)).gameObject.AddComponent<RawImage>();
            meter.raycastTarget = false;
            frame.gameObject.AddComponent<JurorMeterView>().Configure(meter);
        }

        private static void BuildHudRoot(GameObject root, UiTextSO uiText, FallacyCatalogSO catalog, InputActionAsset controls)
        {
            var canvas = ConfigureCanvas(root);
            var strip = CreateStretch(root.transform, "Strip", BottomLeft, BottomRight, Vector2.zero, new Vector2(0f, StripHeight));
            Paint(strip.gameObject, null, TrackColor).raycastTarget = true;

            var help = CreateStretch(strip, "Help", BottomLeft, BottomRight, new Vector2(MessageLeft, Margin), new Vector2(-Margin, Margin + MessageHeight));
            Paint(help.gameObject, UiSprite(MenuBoxSprite), Color.white);
            var reason = Letter(CreateStretch(help, "Reason", BottomLeft, TopRight, new Vector2(DialoguePadding, 0f), new Vector2(-DialoguePadding, -DialogueTopPadding)).gameObject, TextAlignmentOptions.TopLeft);
            reason.color = AccentColor;

            var menu = CreateMenu(strip, reason, uiText, catalog);

            var tier = CreateStretch(strip, "Tier", BottomLeft, BottomRight, new Vector2(MessageLeft, Margin + MessageHeight + Gap), new Vector2(-Margin, Margin + MessageHeight + Gap + TierHeight));
            var player = CreateDebaterPanel(tier, "PlayerPanel", false, PlayerBarSprite, KleiaColor, KleiaDarkColor, uiText);
            var opponent = CreateDebaterPanel(tier, "OpponentPanel", true, ChallengerBarSprite, ChallengerColor, ChallengerDarkColor, uiText);
            var audience = CreateAudience(tier, uiText);

            var area = CreateStretch(strip, "Dialogue", BottomLeft, BottomRight, new Vector2(MessageLeft, Margin), new Vector2(-Margin, Margin + MessageHeight));
            var dialogue = CreateDialogue(area);

            root.AddComponent<BattleMenuInput>().Configure(controls, menu);
            var hud = root.AddComponent<BattleHud>();
            hud.Configure(canvas, player, opponent, audience, menu, dialogue);
            hud.ConfigureDialogueArea(area, MessageLeft, MessageLeft);
        }

        private static void BuildCompendiumRoot(GameObject root, UiTextSO uiText, FallacyCatalogSO catalog, InputActionAsset controls)
        {
            ConfigureCanvas(root);
            var view = CreateCompendiumView(root, uiText, catalog);
            root.AddComponent<CompendiumInput>().Configure(controls, view);
        }

        private static DebaterPanelView CreateDebaterPanel(Transform tier, string name, bool mirrored, string barSprite, Color sideColor, Color faceColor, UiTextSO uiText)
        {
            var near = mirrored ? TopRight : TopLeft;
            var far = mirrored ? TopLeft : TopRight;
            var nearBottom = mirrored ? BottomRight : BottomLeft;
            var toward = mirrored ? -1f : 1f;
            var box = CreateBox(tier, name, nearBottom, Vector2.zero, new Vector2(BadgeWidth, TierHeight));

            var faceFrame = CreateImage(box.transform, "FaceFrame", null, near, new Vector2(toward * BadgeInset, -BadgeInset), new Vector2(FaceFrameSize, FaceFrameSize), sideColor);
            var face = CreateImage(faceFrame.transform, "Face", null, Center, Vector2.zero, new Vector2(FaceSize, FaceSize), faceColor);
            face.gameObject.AddComponent<RectMask2D>();
            var portrait = CreateImage(face.transform, "Portrait", null, TopCenter, Vector2.zero, new Vector2(FaceSize, FaceSize), Color.white);
            portrait.enabled = false;

            var nameLabel = CreatePlate(box.transform, "Name", near, new Vector2(toward * BadgeContent, -BadgeInset), MarkerColor, sideColor);
            nameLabel.fontStyle = FontStyles.UpperCase;
            var caption = CreateLabel(box.transform, "Caption", far, new Vector2(-toward * 4f, -BadgeInset), new Vector2(CaptionWidth, TabHeight), mirrored ? TextAlignmentOptions.Left : TextAlignmentOptions.Right);
            caption.color = CaptionColor;
            caption.gameObject.AddComponent<UiTextLabel>().Configure(caption, uiText, UiTextKeys.CoherenceCaption);

            var bar = CreateBar(box.transform, "Coherence", barSprite, nearBottom, new Vector2(toward * BadgeContent, BadgeInset), mirrored ? Image.OriginHorizontal.Right : Image.OriginHorizontal.Left);
            var value = CreateLabel(box.transform, "Value", nearBottom, new Vector2(toward * (BadgeContent + BarWidth + 3f), 0f), new Vector2(ValueWidth, RowHeight), mirrored ? TextAlignmentOptions.Right : TextAlignmentOptions.Left);
            value.color = MarkerColor;
            var farBottom = mirrored ? BottomLeft : BottomRight;
            var morale = CreateLabel(box.transform, "Morale", farBottom, new Vector2(-toward * 4f, 0f), new Vector2(MoraleWidth, RowHeight), mirrored ? TextAlignmentOptions.Left : TextAlignmentOptions.Right);

            var view = box.gameObject.AddComponent<DebaterPanelView>();
            view.Configure(nameLabel, portrait, bar, value);
            view.ConfigureMorale(morale, uiText, MoraleFavourColor, MoraleAgainstColor);
            return view;
        }

        private static BarView CreateBar(Transform parent, string name, string spriteName, Vector2 anchor, Vector2 position, Image.OriginHorizontal origin)
        {
            var size = new Vector2(BarWidth, BarHeight);
            var track = CreateImage(parent, name, null, anchor, position, size, TrackColor);
            var fill = CreateImage(track.transform, "Fill", UiSprite(spriteName), Center, Vector2.zero, size, Color.white);
            MakeHorizontalFill(fill, origin);
            var flash = CreateImage(track.transform, "Flash", null, Center, Vector2.zero, size, Color.white);
            flash.enabled = false;
            CreateImage(track.transform, "Frame", UiSprite(BarFrameSprite), Center, Vector2.zero, size, Color.white);

            var view = track.gameObject.AddComponent<BarView>();
            view.Configure(fill, flash, BarWidth);
            return view;
        }

        private static AudienceMeterView CreateAudience(Transform tier, UiTextSO uiText)
        {
            var box = CreateBox(tier, "Audience", BottomCenter, Vector2.zero, new Vector2(MeterWidth, TierHeight));
            var caption = CreateLabel(box.transform, "Caption", TopCenter, new Vector2(0f, -BadgeInset), new Vector2(MeterWidth - 2 * DialoguePadding, TabHeight), TextAlignmentOptions.Center);
            caption.color = AccentColor;
            caption.gameObject.AddComponent<UiTextLabel>().Configure(caption, uiText, UiTextKeys.AudienceCaption);

            var size = new Vector2(BarWidth, BarHeight);
            var track = CreateImage(box.transform, "Track", UiSprite(ChallengerBarSprite), BottomCenter, new Vector2(0f, BadgeInset), size, Color.white);
            var fill = CreateImage(track.transform, "PlayerShare", UiSprite(PlayerBarSprite), Center, Vector2.zero, size, Color.white);
            MakeHorizontalFill(fill);
            CreateImage(track.transform, "Frame", UiSprite(BarFrameSprite), Center, Vector2.zero, size, Color.white);
            var marker = CreateImage(track.transform, "Marker", null, Center, Vector2.zero, new Vector2(1f, BarHeight + 2), MarkerColor);

            var playerShare = CreateLabel(box.transform, "PlayerShareValue", BottomLeft, new Vector2(3f, 0f), new Vector2(ShareWidth, RowHeight), TextAlignmentOptions.Left);
            playerShare.color = KleiaLightColor;
            var opponentShare = CreateLabel(box.transform, "OpponentShareValue", BottomRight, new Vector2(-3f, 0f), new Vector2(ShareWidth, RowHeight), TextAlignmentOptions.Right);
            opponentShare.color = ChallengerLightColor;

            var view = box.gameObject.AddComponent<AudienceMeterView>();
            view.Configure(fill, marker.rectTransform, BarWidth);
            view.ConfigureShares(playerShare, opponentShare);
            return view;
        }

        private static ActionMenuView CreateMenu(Transform strip, TMP_Text reason, UiTextSO uiText, FallacyCatalogSO catalog)
        {
            var box = CreateBox(strip, "ActionMenu", BottomLeft, new Vector2(Margin, Margin), new Vector2(MenuWidth, MenuHeight));
            var view = box.gameObject.AddComponent<ActionMenuView>();

            var rows = new TMP_Text[MenuRows];
            for (var index = 0; index < MenuRows; index++)
            {
                rows[index] = CreateRow(box.transform, $"Action{index}", new Vector2(RowInset, -BoxTopPadding - index * RowHeight), MenuWidth - RowInset - 4, view, BattleMenuLayer.Actions, index);
            }

            var cursor = CreateCursor(box.transform);

            var panelHeight = SubmenuTopPadding + RowHeight + SubmenuRows * RowHeight + SubmenuTopPadding;
            var panel = CreateBox(strip, "Submenu", BottomLeft, new Vector2(MessageLeft, StripHeight + Gap), new Vector2(SubmenuWidth, panelHeight));
            var title = CreateLabel(panel.transform, "Title", TopLeft, new Vector2(RowInset, -SubmenuTopPadding), new Vector2(SubmenuTitleWidth, RowHeight), TextAlignmentOptions.Left);
            title.color = AccentColor;
            var options = new TMP_Text[SubmenuCells];
            for (var index = 0; index < SubmenuCells; index++)
            {
                var column = index / SubmenuRows;
                var row = index % SubmenuRows;
                var position = new Vector2(RowInset + column * SubmenuColumnWidth,
                    -SubmenuTopPadding - RowHeight - row * RowHeight);
                options[index] = CreateRow(panel.transform, $"Option{index}", position, SubmenuColumnWidth - RowInset - 4, view, BattleMenuLayer.Fallacies, index);
            }

            var optionCursor = CreateCursor(panel.transform);
            var moreAbove = CreateScrollArrow(panel.transform, "MoreAbove", -SubmenuTopPadding - RowHeight / 2f, UpArrowDegrees);
            var moreBelow = CreateScrollArrow(panel.transform, "MoreBelow", -SubmenuTopPadding - (SubmenuRows + 0.5f) * RowHeight, DownArrowDegrees);
            panel.gameObject.SetActive(false);

            view.Configure(rows, cursor, reason, panel.gameObject, title, options, optionCursor, uiText, catalog);
            view.ConfigureScroll(moreAbove, moreBelow);
            return view;
        }

        private static GameObject CreateScrollArrow(Transform panel, string name, float centerY, float degrees)
        {
            var arrow = CreateImage(panel, name, UiSprite(CursorSprite), TopRight, Vector2.zero, new Vector2(CursorSize, CursorSize), Color.white).rectTransform;
            arrow.pivot = Center;
            arrow.anchoredPosition = new Vector2(-ScrollArrowInset, centerY);
            arrow.localRotation = Quaternion.Euler(0f, 0f, degrees);
            arrow.gameObject.SetActive(false);
            return arrow.gameObject;
        }

        private static DialogueBoxView CreateDialogue(RectTransform area)
        {
            Paint(area.gameObject, UiSprite("box_dialogue"), Color.white);
            var body = Letter(CreateStretch(area, "Body", BottomLeft, TopRight, new Vector2(DialoguePadding, DialogueBottomPadding), new Vector2(-DialoguePadding, -DialogueTopPadding)).gameObject, TextAlignmentOptions.TopLeft, true);
            body.overflowMode = TextOverflowModes.Page;

            var column = CreateRect(area, "Speaker", TopLeft, new Vector2(DialoguePadding, -Margin), new Vector2(PortraitColumnWidth, PortraitSize + 1 + SpeakerPlateHeight));
            var frame = CreateRect(column, "PortraitFrame", TopCenter, Vector2.zero, new Vector2(PortraitSize, PortraitSize));
            Paint(frame.gameObject, UiSprite(PortraitFrameSprite), Color.white);
            var portrait = CreateImage(frame, "Portrait", null, Center, Vector2.zero, new Vector2(PortraitInner, PortraitInner), Color.white);
            portrait.preserveAspect = true;

            var speaker = CreatePlate(column, "Name", BottomCenter, Vector2.zero, InkColor, AccentColor, SpeakerPlateHeight);
            speaker.fontStyle = FontStyles.UpperCase;

            var view = area.gameObject.AddComponent<DialogueBoxView>();
            view.Configure(area.gameObject, body, speaker, portrait, frame.gameObject);
            view.ConfigureMargins(PortraitColumnWidth + DialoguePadding, DialogueBoxView.ChoicePanelWidth + DialoguePadding);
            CreateChoices(area, view);
            area.gameObject.SetActive(false);
            return view;
        }

        private static void CreateChoices(Transform parent, DialogueBoxView view)
        {
            var height = 2 * DialogueBoxView.ChoicePadding + DialogueChoice.MaxOptions * DialogueBoxView.ChoiceRowHeight;
            var panel = CreateBox(parent, "Choices", BottomRight, new Vector2(-4f, ChoiceBottom), new Vector2(DialogueBoxView.ChoicePanelWidth, height));
            var width = DialogueBoxView.ChoicePanelWidth - RowInset - DialogueBoxView.ChoicePadding;
            var (rows, cursor) = CreatePanelRows(panel.transform, DialogueChoice.MaxOptions, new Vector2(RowInset, -DialogueBoxView.ChoicePadding), width, view);
            view.ConfigureChoices(panel.rectTransform, rows, cursor);
            panel.gameObject.SetActive(false);
        }

        private static TMP_Text CreateRow(Transform parent, string name, Vector2 position, float width, ActionMenuView view, BattleMenuLayer layer, int index)
        {
            var label = CreateLabel(parent, name, TopLeft, position, new Vector2(width, RowHeight), TextAlignmentOptions.Left);
            label.raycastTarget = true;
            label.gameObject.AddComponent<MenuRowPointer>().Configure(view, layer, index);
            return label;
        }
    }
}
