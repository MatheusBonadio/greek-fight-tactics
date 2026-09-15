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
        public const string UiSpriteFolder = ArtTools.PlaceholderFolder + "/ui";
        public const int CanvasReference = RenderSetupTools.ReferenceResolution;
        public const int Margin = 8;
        public const int BarWidth = 64;
        public const int BarHeight = 8;
        public const int MenuWidth = 96;
        public const int MenuHeight = 88;
        public const int DialogueReserve = 60;
        public const int MenuRows = 6;
        public const int SubmenuRows = 5;
        public const int RowHeight = 12;
        public const int RowInset = 18;
        public const int FontSize = 8;
        public const int ReasonWidth = 208;

        private static readonly Vector2 TopLeft = new(0f, 1f);
        private static readonly Vector2 TopRight = new(1f, 1f);
        private static readonly Vector2 TopCenter = new(0.5f, 1f);
        private static readonly Vector2 BottomRight = new(1f, 0f);
        private static readonly Vector2 Center = new(0.5f, 0.5f);
        private static readonly Color TrackColor = new Color32(24, 24, 40, 255);
        private static readonly Color OpponentAudienceColor = new Color32(120, 24, 32, 255);
        private static readonly Color MarkerColor = new Color32(248, 248, 240, 255);
        private static readonly Color ReasonColor = new Color32(248, 224, 88, 255);

        public static void BuildHud() => BatchRunner.Run(BuildHudPrefab);

        public static void BuildHudPrefab()
        {
            var uiText = AssetDatabase.LoadAssetAtPath<UiTextSO>(DataTools.UiTextPath)
                ?? throw new InvalidOperationException($"missing {DataTools.UiTextPath}; run DataTools.Regenerate first");
            var catalog = AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath)
                ?? throw new InvalidOperationException($"missing {DataTools.CatalogPath}; run DataTools.Regenerate first");
            var controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(SandboxTools.ControlsPath)
                ?? throw new InvalidOperationException($"missing {SandboxTools.ControlsPath}");

            var root = new GameObject("BattleHud", typeof(RectTransform));
            try
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

                var playerBar = CreateBar(root.transform, "PlayerCoherence", "bar_coherence", TopLeft, new Vector2(Margin, -Margin));
                var opponentBar = CreateBar(root.transform, "OpponentCoherence", "bar_coherence", TopRight, new Vector2(-Margin, -Margin));
                var audience = CreateAudience(root.transform, new Vector2(0f, -Margin));
                var menu = CreateMenu(root.transform, uiText, catalog);

                root.AddComponent<BattleMenuInput>().Configure(controls, menu);
                root.AddComponent<BattleHud>().Configure(canvas, playerBar, opponentBar, audience, menu);

                AssetFolders.Ensure(Path.GetDirectoryName(HudPrefabPath)?.Replace('\\', '/'));
                PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath, out var saved);
                if (!saved)
                {
                    throw new InvalidOperationException($"could not save {HudPrefabPath}");
                }

                Debug.Log($"[BattleHudTools] saved {HudPrefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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
            reason.color = ReasonColor;

            var panel = CreateImage(parent, "Submenu", UiSprite("box_menu"), BottomRight, new Vector2(-Margin - MenuWidth - 4, DialogueReserve), menuSize, Color.white);
            var title = CreateLabel(panel.transform, "Title", TopLeft, new Vector2(Margin, -4f), new Vector2(MenuWidth - 2 * Margin, RowHeight), TextAlignmentOptions.MidlineLeft);
            title.color = ReasonColor;
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

        private static TMP_Text CreateRow(Transform parent, string name, Vector2 position, ActionMenuView view, BattleMenuLayer layer, int index)
        {
            var label = CreateLabel(parent, name, TopLeft, position, new Vector2(MenuWidth - RowInset - 4, RowHeight), TextAlignmentOptions.MidlineLeft);
            label.raycastTarget = true;
            var pointer = label.gameObject.AddComponent<MenuRowPointer>();
            pointer.Configure(view, layer, index);
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

        private static TMP_Text CreateLabel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, TextAlignmentOptions alignment)
        {
            var label = CreateRect(parent, name, anchor, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = FontSize;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Truncate;
            label.raycastTarget = false;
            return label;
        }
    }
}
