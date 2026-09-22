using System;
using System.Collections.Generic;
using System.IO;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    internal static class UiBuilder
    {
        public const string MenuBoxSprite = "box_menu";
        public const string DialogueBoxSprite = "box_dialogue";
        public const string BarFrameSprite = "frame_bar";
        public const string CursorSprite = "cursor";
        public const int CanvasWidth = 640;
        public const int CanvasHeight = 360;
        public const int FontSize = FontTools.NativePixelSize;
        public const int LineHeight = 14;
        public const int RowHeight = 12;
        public const int RowInset = 14;
        public const int CursorSize = 8;
        public const int TabHeight = LineHeight;
        public const int TabPadding = 3;
        public const int TabInset = 8;
        public const int BoxTopPadding = TabHeight / 2;

        public static readonly Vector2 TopLeft = new(0f, 1f);
        public static readonly Vector2 TopRight = new(1f, 1f);
        public static readonly Vector2 TopCenter = new(0.5f, 1f);
        public static readonly Vector2 BottomLeft = new(0f, 0f);
        public static readonly Vector2 BottomRight = new(1f, 0f);
        public static readonly Vector2 BottomCenter = new(0.5f, 0f);
        public static readonly Vector2 Center = new(0.5f, 0.5f);
        public static readonly Color TrackColor = new Color32(0, 0, 0, 255);
        public static readonly Color DimColor = new Color32(0, 0, 0, 208);
        public static readonly Color MarkerColor = new Color32(248, 248, 248, 255);
        public static readonly Color AccentColor = new Color32(248, 216, 48, 255);
        public static readonly Color InkColor = new Color32(8, 8, 16, 255);
        public static readonly Color CaptionColor = new Color32(168, 168, 160, 255);
        public static readonly Color KleiaColor = new Color32(40, 144, 192, 255);
        public static readonly Color KleiaLightColor = new Color32(72, 200, 224, 255);
        public static readonly Color KleiaDarkColor = new Color32(16, 48, 80, 255);
        public static readonly Color ChallengerColor = new Color32(192, 56, 40, 255);
        public static readonly Color ChallengerLightColor = new Color32(248, 120, 88, 255);
        public static readonly Color ChallengerDarkColor = new Color32(120, 24, 32, 255);

        public static Vector2 CanvasSize => new(CanvasWidth, CanvasHeight);

        public static Canvas ConfigureCanvas(GameObject root)
        {
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = CanvasSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            scaler.referencePixelsPerUnit = PixelArtImporter.PixelsPerUnit;
            root.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static T Load<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException($"missing {path}");

        public static Sprite UiSprite(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{BattleHudTools.UiSpriteFolder}/{name}.png")
            ?? throw new InvalidOperationException($"missing ui sprite {name}; run ArtTools.SliceSheets first");

        public static Sprite[] UiSpriteFrames(string name)
        {
            var frames = new List<Sprite> { UiSprite(name) };
            while (AssetDatabase.LoadAssetAtPath<Sprite>($"{BattleHudTools.UiSpriteFolder}/{name}_{frames.Count}.png") is { } frame)
            {
                frames.Add(frame);
            }

            return frames.ToArray();
        }

        public static void SavePrefab(string path, Action<GameObject> build)
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

                Debug.Log($"[UiBuilder] saved {path}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        public static void MakeHorizontalFill(Image image, Image.OriginHorizontal origin = Image.OriginHorizontal.Left)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)origin;
        }

        public static RectTransform CreateRect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
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

        public static RectTransform CreateStretch(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = BottomLeft;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        public static RectTransform Fill(Transform parent, string name) =>
            CreateStretch(parent, name, BottomLeft, TopRight, Vector2.zero, Vector2.zero);

        public static Image CreateImage(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 position, Vector2 size, Color color) =>
            Paint(CreateRect(parent, name, anchor, position, size).gameObject, sprite, color);

        public static Image Paint(GameObject target, Sprite sprite, Color color)
        {
            var image = target.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            if (sprite != null && sprite.border != Vector4.zero)
            {
                image.type = Image.Type.Sliced;
            }

            return image;
        }

        public static Image CreateBox(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size) =>
            CreateImage(parent, name, UiSprite(MenuBoxSprite), anchor, position, size, Color.white);

        public static TMP_Text CreateLabel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, TextAlignmentOptions alignment, bool wrap = false) =>
            Letter(CreateRect(parent, name, anchor, position, size).gameObject, alignment, wrap);

        public static TMP_Text Letter(GameObject target, TextAlignmentOptions alignment, bool wrap = false)
        {
            var label = target.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = FontSize;
            label.alignment = alignment;
            label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.enableAutoSizing = false;
            label.overflowMode = wrap ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
            label.raycastTarget = false;
            label.color = MarkerColor;
            return label;
        }

        public static TMP_Text CreateTab(Transform parent, string name, Vector2 anchor, float inset, Color textColor, Color plateColor) =>
            CreatePlate(parent, name, anchor, new Vector2(anchor.x < 0.5f ? inset : -inset, TabHeight / 2f), textColor, plateColor);

        public static TMP_Text CreatePlate(Transform parent, string name, Vector2 anchor, Vector2 position, Color textColor, Color plateColor, float height = TabHeight)
        {
            var plate = CreateRect(parent, name, anchor, position, new Vector2(0f, height));
            var background = Paint(plate.gameObject, null, plateColor);
            var label = Letter(Fill(plate, "Label").gameObject, AlignmentFor(anchor));
            var rect = label.rectTransform;
            rect.offsetMin = new Vector2(TabPadding, 0f);
            rect.offsetMax = new Vector2(-TabPadding, 0f);
            label.color = textColor;
            plate.gameObject.AddComponent<UiPlate>().Configure(label, background, TabPadding);
            return label;
        }

        public static TextAlignmentOptions AlignmentFor(Vector2 anchor) =>
            anchor.x < 0.5f ? TextAlignmentOptions.Left : anchor.x > 0.5f ? TextAlignmentOptions.Right : TextAlignmentOptions.Center;

        public static RectTransform CreateCursor(Transform parent) =>
            CreateImage(parent, "Cursor", UiSprite(CursorSprite), TopLeft, Vector2.zero, new Vector2(CursorSize, CursorSize), Color.white).rectTransform;

        public static (TMP_Text[] Rows, RectTransform Cursor) CreatePanelRows(Transform parent, int count, Vector2 origin, float width, MonoBehaviour panel)
        {
            var rows = new TMP_Text[count];
            for (var index = 0; index < count; index++)
            {
                rows[index] = CreateLabel(parent, $"Row{index}", TopLeft, new Vector2(origin.x, origin.y - index * RowHeight), new Vector2(width, RowHeight), TextAlignmentOptions.Left);
                rows[index].raycastTarget = true;
                rows[index].gameObject.AddComponent<PanelRowPointer>().Configure(panel, index);
            }

            return (rows, CreateCursor(parent));
        }
    }
}
