using TMPro;
using UnityEngine;

namespace Game.UI
{
    public static class UiCursor
    {
        public static bool IsBlinkOn(float clock) => Mathf.FloorToInt(clock / ActionMenuView.CursorBlinkSeconds) % 2 == 0;

        public static void Place(RectTransform cursor, TMP_Text row, bool visible)
        {
            if (!cursor)
            {
                return;
            }

            var shown = visible && row;
            cursor.gameObject.SetActive(shown);
            if (shown)
            {
                var rowPosition = row.rectTransform.anchoredPosition;
                cursor.anchoredPosition = new Vector2(rowPosition.x + ActionMenuView.CursorOffsetX, rowPosition.y + ActionMenuView.CursorOffsetY);
            }
        }
    }
}
