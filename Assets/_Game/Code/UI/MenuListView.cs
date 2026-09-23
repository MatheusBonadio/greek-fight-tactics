using System;
using System.Collections.Generic;
using Game.Content;
using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    public sealed class MenuListView : MonoBehaviour, IMenuPanel
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text[] rows = Array.Empty<TMP_Text>();
        [SerializeField] private RectTransform cursor;
        [SerializeField] private UiTextSO text;
        [SerializeField] private string titleKey;
        [SerializeField] private Color availableColor = new Color32(248, 248, 248, 255);
        [SerializeField] private Color unavailableColor = new Color32(120, 120, 128, 255);

        private float blinkClock;

        public event Action<string> Chosen;

        public event Action Cancelled;

        public MenuListModel Model { get; private set; }

        public IReadOnlyList<TMP_Text> Rows => rows;

        public RectTransform Cursor => cursor;

        public Color UnavailableColor => unavailableColor;

        public bool CursorVisible { get; private set; } = true;

        public void Configure(TMP_Text titleLabel, string titleTextKey, TMP_Text[] rowLabels, RectTransform rowCursor, UiTextSO uiText)
        {
            title = titleLabel;
            titleKey = titleTextKey;
            rows = rowLabels;
            cursor = rowCursor;
            text = uiText;
        }

        public void Bind(MenuListModel model)
        {
            Model = model;
            RestartBlink();
        }

        public void HandleNavigate(Vector2 direction)
        {
            if (Model == null || Mathf.Approximately(direction.y, 0f))
            {
                return;
            }

            Model.Move(direction.y > 0f ? -1 : 1);
            GameAudio.PlayUi(AudioIds.Cursor);
            RestartBlink();
        }

        public void HandleConfirm()
        {
            if (Model != null && Model.TryConfirm(out var id))
            {
                GameAudio.PlayUi(AudioIds.Confirm);
                Chosen?.Invoke(id);
            }
        }

        public void HandleCancel()
        {
            GameAudio.PlayUi(AudioIds.Cancel);
            Cancelled?.Invoke();
        }

        public void HandlePoint(int row)
        {
            if (Model != null && Model.Point(row))
            {
                RestartBlink();
            }
        }

        public void Advance(float deltaSeconds)
        {
            blinkClock += Mathf.Max(0f, deltaSeconds);
            var visible = UiCursor.IsBlinkOn(blinkClock);
            if (visible != CursorVisible)
            {
                CursorVisible = visible;
                Render();
            }
        }

        public void Render()
        {
            if (Model == null)
            {
                return;
            }

            if (title)
            {
                title.text = string.IsNullOrEmpty(titleKey) ? string.Empty : text.Get(titleKey);
            }

            var entries = Model.Entries;
            for (var index = 0; index < rows.Length; index++)
            {
                var used = index < entries.Count;
                rows[index].gameObject.SetActive(used);
                if (used)
                {
                    rows[index].text = text.Get(entries[index].LabelKey);
                    rows[index].color = entries[index].Enabled ? availableColor : unavailableColor;
                }
            }

            UiCursor.Place(cursor, Model.Cursor < rows.Length ? rows[Model.Cursor] : null, CursorVisible);
        }

        private void Update()
        {
            Advance(Time.unscaledDeltaTime);
        }

        private void RestartBlink()
        {
            blinkClock = 0f;
            CursorVisible = true;
            Render();
        }
    }
}
