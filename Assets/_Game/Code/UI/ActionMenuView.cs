using System;
using System.Collections.Generic;
using Game.Content;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    public sealed class ActionMenuView : MonoBehaviour
    {
        public const float CursorBlinkSeconds = 0.5f;
        public const float CursorOffsetX = -10f;
        public const float CursorOffsetY = -2f;

        [SerializeField] private TMP_Text[] actionRows = Array.Empty<TMP_Text>();
        [SerializeField] private RectTransform actionCursor;
        [SerializeField] private TMP_Text reasonLabel;
        [SerializeField] private GameObject submenuPanel;
        [SerializeField] private TMP_Text submenuTitle;
        [SerializeField] private TMP_Text[] submenuRows = Array.Empty<TMP_Text>();
        [SerializeField] private RectTransform submenuCursor;
        [SerializeField] private UiTextSO text;
        [SerializeField] private FallacyCatalogSO catalog;
        [SerializeField] private Color availableColor = new Color32(248, 248, 240, 255);
        [SerializeField] private Color unavailableColor = new Color32(120, 120, 128, 255);

        private float blinkClock;

        public event Action<BattleMenuChoice> ChoiceMade;

        public BattleMenuController Controller { get; private set; }

        public IReadOnlyList<TMP_Text> ActionRows => actionRows;

        public IReadOnlyList<TMP_Text> SubmenuRows => submenuRows;

        public TMP_Text ReasonLabel => reasonLabel;

        public TMP_Text SubmenuTitle => submenuTitle;

        public GameObject SubmenuPanel => submenuPanel;

        public RectTransform ActionCursor => actionCursor;

        public Color UnavailableColor => unavailableColor;

        public Color AvailableColor => availableColor;

        public int SubmenuWindowStart { get; private set; }

        public bool CursorVisible { get; private set; } = true;

        public static int WindowStart(int cursor, int count, int visible) =>
            count <= visible ? 0 : Mathf.Clamp(cursor - visible + 1, 0, count - visible);

        public void Configure(
            TMP_Text[] rows,
            RectTransform cursor,
            TMP_Text reason,
            GameObject panel,
            TMP_Text title,
            TMP_Text[] optionRows,
            RectTransform optionCursor,
            UiTextSO uiText,
            FallacyCatalogSO fallacies)
        {
            actionRows = rows;
            actionCursor = cursor;
            reasonLabel = reason;
            submenuPanel = panel;
            submenuTitle = title;
            submenuRows = optionRows;
            submenuCursor = optionCursor;
            text = uiText;
            catalog = fallacies;
        }

        public void Bind(BattleMenuController controller)
        {
            Controller = controller;
            blinkClock = 0f;
            CursorVisible = true;
            Render();
        }

        public void HandleNavigate(Vector2 direction)
        {
            if (Controller == null || Mathf.Approximately(direction.y, 0f))
            {
                return;
            }

            Controller.Navigate(direction.y > 0f ? -1 : 1);
            RestartBlink();
        }

        public void HandleConfirm()
        {
            if (Controller == null)
            {
                return;
            }

            var chosen = Controller.TryConfirm(out var choice);
            RestartBlink();
            if (chosen)
            {
                ChoiceMade?.Invoke(choice);
            }
        }

        public void HandleCancel()
        {
            if (Controller != null && Controller.Cancel())
            {
                RestartBlink();
            }
        }

        public void HandlePoint(BattleMenuLayer layer, int row)
        {
            if (Controller == null || layer != Controller.Layer)
            {
                return;
            }

            Controller.Point(layer == BattleMenuLayer.Actions ? row : SubmenuWindowStart + row);
            RestartBlink();
        }

        public void HandleClick(BattleMenuLayer layer, int row)
        {
            if (Controller == null || layer != Controller.Layer)
            {
                return;
            }

            HandlePoint(layer, row);
            HandleConfirm();
        }

        public void Advance(float deltaSeconds)
        {
            blinkClock += Mathf.Max(0f, deltaSeconds);
            var visible = Mathf.FloorToInt(blinkClock / CursorBlinkSeconds) % 2 == 0;
            if (visible != CursorVisible)
            {
                CursorVisible = visible;
                Render();
            }
        }

        public void Render()
        {
            if (Controller == null)
            {
                return;
            }

            RenderActions();
            var inSubmenu = Controller.Layer != BattleMenuLayer.Actions;
            submenuPanel.SetActive(inSubmenu);
            if (inSubmenu)
            {
                RenderSubmenu();
            }
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

        private void RenderActions()
        {
            var entries = Controller.Actions.Entries;
            for (var index = 0; index < actionRows.Length; index++)
            {
                var row = actionRows[index];
                var used = index < entries.Count;
                row.gameObject.SetActive(used);
                if (used)
                {
                    row.text = text.Get(UiTextKeys.For(entries[index].Action));
                    row.color = entries[index].IsAvailable ? availableColor : unavailableColor;
                }
            }

            var inActions = Controller.Layer == BattleMenuLayer.Actions;
            PlaceCursor(actionCursor, actionRows, Controller.Actions.CursorIndex, inActions);

            var selected = Controller.Actions.Selected;
            reasonLabel.text = inActions && !selected.IsAvailable ? text.Get(UiTextKeys.For(selected.Reason)) : string.Empty;
        }

        private void RenderSubmenu()
        {
            submenuTitle.text = text.Get(Controller.Layer == BattleMenuLayer.Fallacies ? UiTextKeys.ChooseFallacy : UiTextKeys.ChoosePremise);

            var options = Controller.SubmenuOptions;
            SubmenuWindowStart = WindowStart(Controller.SubmenuCursor, options.Count, submenuRows.Length);
            for (var row = 0; row < submenuRows.Length; row++)
            {
                var optionIndex = SubmenuWindowStart + row;
                var used = optionIndex < options.Count;
                submenuRows[row].gameObject.SetActive(used);
                if (used)
                {
                    submenuRows[row].text = OptionLabel(options[optionIndex]);
                    submenuRows[row].color = availableColor;
                }
            }

            PlaceCursor(submenuCursor, submenuRows, Controller.SubmenuCursor - SubmenuWindowStart, true);
        }

        private string OptionLabel(string option) =>
            Controller.Layer == BattleMenuLayer.Fallacies && catalog && catalog.TryGet(option, out var fallacy) ? fallacy.DisplayName : option;

        private void PlaceCursor(RectTransform cursor, IReadOnlyList<TMP_Text> rows, int index, bool layerActive)
        {
            if (!cursor)
            {
                return;
            }

            var visible = layerActive && CursorVisible && index >= 0 && index < rows.Count;
            cursor.gameObject.SetActive(visible);
            if (visible)
            {
                var rowPosition = rows[index].rectTransform.anchoredPosition;
                cursor.anchoredPosition = new Vector2(rowPosition.x + CursorOffsetX, rowPosition.y + CursorOffsetY);
            }
        }
    }
}
