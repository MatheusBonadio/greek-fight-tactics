using System;
using System.Collections.Generic;
using Game.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class CompendiumView : MonoBehaviour
    {
        public const string HeaderKey = "compendium.title";
        public const string CounterKey = "compendium.counter";
        public const string EmptyKey = "compendium.empty";

        [SerializeField] private TMP_Text header;
        [SerializeField] private TMP_Text[] rows = Array.Empty<TMP_Text>();
        [SerializeField] private RectTransform cursor;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text description;
        [SerializeField] private TMP_Text hint;
        [SerializeField] private TMP_Text counter;
        [SerializeField] private Image icon;
        [SerializeField] private FallacyCatalogSO catalog;
        [SerializeField] private UiTextSO text;

        private IReadOnlyList<string> unlockedIds = Array.Empty<string>();

        public event Action Closed;

        public FallacyCompendium Compendium { get; private set; }

        public int Cursor { get; private set; }

        public int WindowStart { get; private set; }

        public string SelectedId => unlockedIds.Count > 0 ? unlockedIds[Cursor] : null;

        public IReadOnlyList<TMP_Text> Rows => rows;

        public TMP_Text Title => title;

        public TMP_Text Description => description;

        public TMP_Text Hint => hint;

        public TMP_Text Counter => counter;

        public Image Icon => icon;

        public void Configure(
            TMP_Text headerLabel,
            TMP_Text[] rowLabels,
            RectTransform rowCursor,
            TMP_Text titleLabel,
            TMP_Text descriptionLabel,
            TMP_Text hintLabel,
            TMP_Text counterLabel,
            Image iconImage,
            FallacyCatalogSO fallacies,
            UiTextSO uiText)
        {
            header = headerLabel;
            rows = rowLabels;
            cursor = rowCursor;
            title = titleLabel;
            description = descriptionLabel;
            hint = hintLabel;
            counter = counterLabel;
            icon = iconImage;
            catalog = fallacies;
            text = uiText;
        }

        public void Bind(FallacyCompendium compendium)
        {
            Compendium = compendium;
            Cursor = 0;
            Render();
        }

        public void HandleNavigate(Vector2 direction)
        {
            if (Compendium == null || unlockedIds.Count == 0 || Mathf.Approximately(direction.y, 0f))
            {
                return;
            }

            var count = unlockedIds.Count;
            Cursor = ((Cursor + (direction.y > 0f ? -1 : 1)) % count + count) % count;
            Render();
        }

        public void HandlePoint(int row)
        {
            var index = WindowStart + row;
            if (Compendium != null && row >= 0 && index < unlockedIds.Count)
            {
                Cursor = index;
                Render();
            }
        }

        public void HandleCancel()
        {
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        public void Render()
        {
            if (Compendium == null)
            {
                return;
            }

            unlockedIds = Compendium.UnlockedIds;
            Cursor = unlockedIds.Count == 0 ? 0 : Mathf.Clamp(Cursor, 0, unlockedIds.Count - 1);
            WindowStart = ActionMenuView.WindowStart(Cursor, unlockedIds.Count, rows.Length);

            header.text = text.Get(HeaderKey);
            counter.text = string.Format(text.Get(CounterKey), unlockedIds.Count, Compendium.Total);
            RenderRows();
            RenderDetail();
        }

        private void RenderRows()
        {
            for (var row = 0; row < rows.Length; row++)
            {
                var index = WindowStart + row;
                var used = index < unlockedIds.Count;
                rows[row].gameObject.SetActive(used);
                if (used)
                {
                    rows[row].text = catalog.TryGet(unlockedIds[index], out var fallacy) ? fallacy.DisplayName : unlockedIds[index];
                }
            }

            var cursorRow = Cursor - WindowStart;
            var visible = cursor && unlockedIds.Count > 0 && cursorRow >= 0 && cursorRow < rows.Length;
            if (cursor)
            {
                cursor.gameObject.SetActive(visible);
            }

            if (visible)
            {
                var rowPosition = rows[cursorRow].rectTransform.anchoredPosition;
                cursor.anchoredPosition = new Vector2(rowPosition.x + ActionMenuView.CursorOffsetX, rowPosition.y + ActionMenuView.CursorOffsetY);
            }
        }

        private void RenderDetail()
        {
            if (SelectedId == null || !catalog.TryGet(SelectedId, out var fallacy))
            {
                title.text = unlockedIds.Count == 0 ? text.Get(EmptyKey) : SelectedId;
                description.text = string.Empty;
                hint.text = string.Empty;
                icon.enabled = false;
                return;
            }

            title.text = fallacy.DisplayName;
            description.text = fallacy.Description;
            hint.text = fallacy.PlayerHint;
            icon.sprite = fallacy.Icon;
            icon.enabled = fallacy.Icon != null;
        }
    }
}
