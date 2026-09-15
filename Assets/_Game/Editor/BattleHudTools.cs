using System;
using System.IO;
using Game.Content;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    public static class BattleHudTools
    {
        public const string HudPrefabPath = "Assets/_Game/Prefabs/UI/BattleHud.prefab";
        public const string CompendiumPrefabPath = "Assets/_Game/Prefabs/UI/Compendium.prefab";
        public const string UiSpriteFolder = ArtTools.PlaceholderFolder + "/ui";
        public const int CanvasReference = RenderSetupTools.ReferenceResolution;
        public const int Margin = 8;
        public const int BarWidth = 64;
        public const int BarHeight = 8;
        public const int MenuWidth = 96;
        public const int MenuHeight = 88;
        public const int DialogueWidth = DialogueBoxView.BoxPixelWidth;
        public const int DialogueHeight = 48;
        public const int DialoguePadding = 8;
        public const int PortraitSize = 40;
        public const int PortraitInner = 32;
        public const int DialogueReserve = Margin + DialogueHeight + 4;
        public const int MenuRows = 6;
        public const int SubmenuRows = 5;
        public const int CompendiumRows = 12;
        public const int RowHeight = 12;
        public const int RowInset = 18;
        public const int FontSize = 8;
        public const int ReasonWidth = 208;

        private static readonly Vector2 TopLeft = new(0f, 1f);
        private static readonly Vector2 TopRight = new(1f, 1f);
        private static readonly Vector2 TopCenter = new(0.5f, 1f);
        private static readonly Vector2 BottomLeft = new(0f, 0f);
        private static readonly Vector2 BottomRight = new(1f, 0f);
        private static readonly Vector2 BottomCenter = new(0.5f, 0f);
        private static readonly Vector2 Center = new(0.5f, 0.5f);
        private static readonly Color TrackColor = new Color32(24, 24, 40, 255);
        private static readonly Color OpponentAudienceColor = new Color32(120, 24, 32, 255);
        private static readonly Color MarkerColor = new Color32(248, 248, 240, 255);
        private static readonly Color AccentColor = new Color32(248, 224, 88, 255);

        public static void BuildHud() => BatchRunner.Run(BuildHudPrefabs);

        public static void BuildHudPrefabs()
        {
            var uiText = AssetDatabase.LoadAssetAtPath<UiTextSO>(DataTools.UiTextPath)
                ?? throw new InvalidOperationException($"missing {DataTools.UiTextPath}; run DataTools.Regenerate first");
            var catalog = AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath)
                ?? throw new InvalidOperationException($"missing {DataTools.CatalogPath}; run DataTools.Regenerate first");
            var controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(SandboxTools.ControlsPath)
                ?? throw new InvalidOperationException($"missing {SandboxTools.ControlsPath}");

            SavePrefab(HudPrefabPath, root => BuildHudRoot(root, uiText, catalog, controls));
            SavePrefab(CompendiumPrefabPath, root => BuildCompendiumRoot(root, uiText, catalog, controls));
        }

        private static void BuildHudRoot(GameObject root, UiTextSO uiText, FallacyCatalogSO catalog, InputActionAsset controls)
        {
            var canvas = ConfigureCanvas(root);
            var playerBar = CreateBar(root.transform, "PlayerCoherence", "bar_coherence", TopLeft, new Vector2(Margin, -Margin));
            var opponentBar = CreateBar(root.transform, "OpponentCoherence", "bar_coherence", TopRight, new Vector2(-Margin, -Margin));
            var audience = CreateAudience(root.transform, new Vector2(0f, -Margin));
            var menu = CreateMenu(root.transform, uiText, catalog);
            var dialogue = CreateDialogue(root.transform);

            root.AddComponent<BattleMenuInput>().Configure(controls, menu);
            root.AddComponent<BattleHud>().Configure(canvas, playerBar, opponentBar, audience, menu, dialogue);
        }

        private static void BuildCompendiumRoot(GameObject root, UiTextSO uiText, FallacyCatalogSO catalog, InputActionAsset controls)
        {
            ConfigureCanvas(root);
            var panel = CreateImage(root.transform, "Panel", null, Center, Vector2.zero, new Vector2(CanvasReference, CanvasReference), TrackColor);

            var header = CreateLabel(panel.transform, "Header", TopLeft, new Vector2(Margin, -4f), new Vector2(CanvasReference - 2 * Margin, RowHeight), TextAlignmentOptions.MidlineLeft);
            header.color = AccentColor;
            var rows = new TMP_Text[CompendiumRows];
            for (var index = 0; index < CompendiumRows; index++)
            {
                rows[index] = CreateLabel(panel.transform, $"Entry{index}", TopLeft, new Vector2(RowInset, -4f - (index + 1) * RowHeight), new Vector2(92f, RowHeight), TextAlignmentOptions.MidlineLeft);
            }

            var cursor = CreateImage(panel.transform, "Cursor", UiSprite("cursor"), TopLeft, Vector2.zero, new Vector2(8f, 8f), Color.white).rectTransform;
            var icon = CreateImage(panel.transform, "Icon", null, TopLeft, new Vector2(116f, -4f - RowHeight), new Vector2(16f, 16f), Color.white);
            var title = CreateLabel(panel.transform, "Title", TopLeft, new Vector2(136f, -4f - RowHeight), new Vector2(80f, 16f), TextAlignmentOptions.MidlineLeft);
            title.color = AccentColor;
            var description = CreateLabel(panel.transform, "Description", TopLeft, new Vector2(116f, -36f), new Vector2(100f, 80f), TextAlignmentOptions.TopLeft, true);
            var hint = CreateLabel(panel.transform, "Hint", TopLeft, new Vector2(116f, -120f), new Vector2(100f, 72f), TextAlignmentOptions.TopLeft, true);
            hint.color = AccentColor;
            var counter = CreateLabel(panel.transform, "Counter", BottomLeft, new Vector2(Margin, 4f), new Vector2(100f, RowHeight), TextAlignmentOptions.MidlineLeft);

            var view = root.AddComponent<CompendiumView>();
            view.Configure(header, rows, cursor, title, description, hint, counter, icon, catalog, uiText);
            root.AddComponent<CompendiumInput>().Configure(controls, view);
        }

        private static void SavePrefab(string path, Action<GameObject> build)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(path), typeof(RectTransform));
            try
            {
                build(root);
                AssetFolders.Ensure(Path.GetDirectoryName(path)?.Replace('\\', '/'));
                PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
                if (!saved)
                {
                    throw new InvalidOperationException($"could not save {path}");
                }

                Debug.Log($"[BattleHudTools] saved {path}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Canvas ConfigureCanvas(GameObject root)
        {
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.planeDistance = 1f;
            canvas.pixelPerfect = true;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(CanvasReference, CanvasReference);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            scaler.referencePixelsPerUnit = PixelArtImporter.PixelsPerUnit;
            root.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static BarView CreateBar(Transform parent, string name, string spriteName, Vector2 anchor, Vector2 position)
        {
            var size = new Vector2(BarWidth, BarHeight);
            var track = CreateImage(parent, name, null, anchor, position, size, TrackColor);
            var fill = CreateImage(track.transform, "Fill", UiSprite(spriteName), Center, Vector2.zero, size, Color.white);
            MakeHorizontalFill(fill);
            var flash = CreateImage(track.transform, "Flash", null, Center, Vector2.zero, size, Color.white);
            flash.enabled = false;

            var view = track.gameObject.AddComponent<BarView>();
            view.Configure(fill, flash, BarWidth);
            return view;
        }

        private static AudienceMeterView CreateAudience(Transform parent, Vector2 position)
        {
            var size = new Vector2(BarWidth, BarHeight);
            var track = CreateImage(parent, "Audience", null, TopCenter, position, size, OpponentAudienceColor);
            var fill = CreateImage(track.transform, "PlayerShare", UiSprite("bar_audience"), Center, Vector2.zero, size, Color.white);
            MakeHorizontalFill(fill);
            var marker = CreateImage(track.transform, "Marker", null, Center, Vector2.zero, new Vector2(1f, BarHeight + 2), MarkerColor);

            var view = track.gameObject.AddComponent<AudienceMeterView>();
            view.Configure(fill, marker.rectTransform, BarWidth);
            return view;
        }

        private static ActionMenuView CreateMenu(Transform parent, UiTextSO uiText, FallacyCatalogSO catalog)
        {
            var menuSize = new Vector2(MenuWidth, MenuHeight);
            var box = CreateImage(parent, "ActionMenu", UiSprite("box_menu"), BottomRight, new Vector2(-Margin, DialogueReserve), menuSize, Color.white);
            var view = box.gameObject.AddComponent<ActionMenuView>();

            var rows = new TMP_Text[MenuRows];
            for (var index = 0; index < MenuRows; index++)
            {
                rows[index] = CreateRow(box.transform, $"Action{index}", new Vector2(RowInset, -Margin - index * RowHeight), view, BattleMenuLayer.Actions, index);
            }

            var cursor = CreateImage(box.transform, "Cursor", UiSprite("cursor"), TopLeft, Vector2.zero, new Vector2(8f, 8f), Color.white).rectTransform;
            var reason = CreateLabel(parent, "Reason", BottomRight, new Vector2(-Margin, DialogueReserve + MenuHeight + 2), new Vector2(ReasonWidth, RowHeight), TextAlignmentOptions.MidlineRight);
            reason.color = AccentColor;

            var panel = CreateImage(parent, "Submenu", UiSprite("box_menu"), BottomRight, new Vector2(-Margin - MenuWidth - 4, DialogueReserve), menuSize, Color.white);
            var title = CreateLabel(panel.transform, "Title", TopLeft, new Vector2(Margin, -4f), new Vector2(MenuWidth - 2 * Margin, RowHeight), TextAlignmentOptions.MidlineLeft);
            title.color = AccentColor;
            var options = new TMP_Text[SubmenuRows];
            for (var index = 0; index < SubmenuRows; index++)
            {
                options[index] = CreateRow(panel.transform, $"Option{index}", new Vector2(RowInset, -4f - (index + 1) * RowHeight), view, BattleMenuLayer.Fallacies, index);
            }

            var optionCursor = CreateImage(panel.transform, "Cursor", UiSprite("cursor"), TopLeft, Vector2.zero, new Vector2(8f, 8f), Color.white).rectTransform;
            panel.gameObject.SetActive(false);

            view.Configure(rows, cursor, reason, panel.gameObject, title, options, optionCursor, uiText, catalog);
            return view;
        }

        private static DialogueBoxView CreateDialogue(Transform parent)
        {
            var container = CreateRect(parent, "Dialogue", Center, Vector2.zero, new Vector2(CanvasReference, CanvasReference)).gameObject;
            var box = CreateImage(container.transform, "Box", UiSprite("box_dialogue"), BottomCenter, new Vector2(0f, Margin), new Vector2(DialogueWidth, DialogueHeight), Color.white);
            var body = CreateLabel(box.transform, "Body", TopLeft, new Vector2(DialoguePadding, -DialoguePadding / 2f), new Vector2(DialogueWidth - 2 * DialoguePadding, DialogueHeight - DialoguePadding), TextAlignmentOptions.TopLeft, true);
            body.overflowMode = TextOverflowModes.Page;

            var frame = CreateImage(container.transform, "PortraitFrame", UiSprite("frame_portrait"), BottomLeft, new Vector2(Margin, Margin + DialogueHeight + 2), new Vector2(PortraitSize, PortraitSize), Color.white);
            var portrait = CreateImage(frame.transform, "Portrait", null, Center, Vector2.zero, new Vector2(PortraitInner, PortraitInner), Color.white);
            portrait.preserveAspect = true;
            var speaker = CreateLabel(container.transform, "Speaker", BottomLeft, new Vector2(Margin + PortraitSize + 4, Margin + DialogueHeight + 2), new Vector2(120f, RowHeight), TextAlignmentOptions.MidlineLeft);
            speaker.color = AccentColor;

            var view = container.AddComponent<DialogueBoxView>();
            view.Configure(container, body, speaker, portrait, frame.gameObject);
            container.SetActive(false);
            return view;
        }

        private static TMP_Text CreateRow(Transform parent, string name, Vector2 position, ActionMenuView view, BattleMenuLayer layer, int index)
        {
            var label = CreateLabel(parent, name, TopLeft, position, new Vector2(MenuWidth - RowInset - 4, RowHeight), TextAlignmentOptions.MidlineLeft);
            label.raycastTarget = true;
            label.gameObject.AddComponent<MenuRowPointer>().Configure(view, layer, index);
            return label;
        }

        private static Sprite UiSprite(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{UiSpriteFolder}/{name}.png")
            ?? throw new InvalidOperationException($"missing ui sprite {name}; run ArtTools.SliceSheets first");

        private static void MakeHorizontalFill(Image image)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        private static RectTransform CreateRect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image CreateImage(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var image = CreateRect(parent, name, anchor, position, size).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateLabel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, TextAlignmentOptions alignment, bool wrap = false)
        {
            var label = CreateRect(parent, name, anchor, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = FontSize;
            label.alignment = alignment;
            label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Truncate;
            label.raycastTarget = false;
            return label;
        }
    }
}
