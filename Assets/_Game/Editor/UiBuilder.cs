using System;
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
        public static readonly Vector2 TopLeft = new(0f, 1f);
        public static readonly Vector2 TopRight = new(1f, 1f);
        public static readonly Vector2 TopCenter = new(0.5f, 1f);
        public static readonly Vector2 BottomLeft = new(0f, 0f);
        public static readonly Vector2 BottomRight = new(1f, 0f);
        public static readonly Vector2 BottomCenter = new(0.5f, 0f);
        public static readonly Vector2 Center = new(0.5f, 0.5f);
        public static readonly Color TrackColor = new Color32(24, 24, 40, 255);
        public static readonly Color PanelColor = new Color32(16, 48, 80, 255);
        public static readonly Color DimColor = new Color32(8, 8, 16, 208);
        public static readonly Color OpponentAudienceColor = new Color32(120, 24, 32, 255);
        public static readonly Color MarkerColor = new Color32(248, 248, 240, 255);
        public static readonly Color AccentColor = new Color32(248, 224, 88, 255);

        public static Canvas ConfigureCanvas(GameObject root)
        {
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(BattleHudTools.CanvasReference, BattleHudTools.CanvasReference);
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

        public static void MakeHorizontalFill(Image image)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
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

        public static Image CreateImage(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var image = CreateRect(parent, name, anchor, position, size).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static TMP_Text CreateLabel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, TextAlignmentOptions alignment, bool wrap = false)
        {
            var label = CreateRect(parent, name, anchor, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = BattleHudTools.FontSize;
            label.alignment = alignment;
            label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.enableAutoSizing = !wrap;
            label.fontSizeMin = BattleHudTools.MinFontSize;
            label.fontSizeMax = BattleHudTools.FontSize;
            label.overflowMode = TextOverflowModes.Truncate;
            label.raycastTarget = false;
            return label;
        }

        public static (TMP_Text[] Rows, RectTransform Cursor) CreatePanelRows(Transform parent, int count, Vector2 origin, float width, MonoBehaviour panel)
        {
            var rows = new TMP_Text[count];
            for (var index = 0; index < count; index++)
            {
                rows[index] = CreateLabel(parent, $"Row{index}", TopLeft, new Vector2(origin.x, origin.y - index * BattleHudTools.RowHeight), new Vector2(width, BattleHudTools.RowHeight), TextAlignmentOptions.Left);
                rows[index].raycastTarget = true;
                rows[index].gameObject.AddComponent<PanelRowPointer>().Configure(panel, index);
            }

            var cursor = CreateImage(parent, "Cursor", UiSprite("cursor"), TopLeft, Vector2.zero, new Vector2(8f, 8f), Color.white).rectTransform;
            return (rows, cursor);
        }
    }
}
