using System;
using System.Collections.Generic;
using Game.Content;
using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    public sealed class ActionMenuView : MonoBehaviour
    {
        public const float CursorBlinkSeconds = 0.5f;
        public const float CursorOffsetX = -8f;
        public const float CursorOffsetY = -3f;

        [SerializeField] private TMP_Text[] actionRows = Array.Empty<TMP_Text>();
        [SerializeField] private RectTransform actionCursor;
        [SerializeField] private TMP_Text reasonLabel;
        [SerializeField] private GameObject submenuPanel;
        [SerializeField] private TMP_Text submenuTitle;
        [SerializeField] private TMP_Text[] submenuRows = Array.Empty<TMP_Text>();
        [SerializeField] private RectTransform submenuCursor;
        [SerializeField] private GameObject moreAbove;
        [SerializeField] private GameObject moreBelow;
        [SerializeField] private UiTextSO text;
        [SerializeField] private FallacyCatalogSO catalog;
        [SerializeField] private Color availableColor = new Color32(248, 248, 248, 255);
        [SerializeField] private Color unavailableColor = new Color32(120, 120, 128, 255);
        [SerializeField] private Color hintColor = new Color32(248, 216, 48, 255);
        [SerializeField] private bool assistedMode;

        private float blinkClock;
        private BattleMenuController notifiedController;
        private ActionMenuEntry? notifiedEntry;

        public event Action<BattleMenuChoice> ChoiceMade;

        public event Action<ActionMenuEntry> SelectionChanged;

        public event Action<bool> SubmenuToggled;

        public BattleMenuController Controller { get; private set; }

        public IReadOnlyList<TMP_Text> ActionRows => actionRows;

        public IReadOnlyList<TMP_Text> SubmenuRows => submenuRows;

        public TMP_Text ReasonLabel => reasonLabel;

        public TMP_Text SubmenuTitle => submenuTitle;

        public GameObject SubmenuPanel => submenuPanel;

        public RectTransform ActionCursor => actionCursor;

        public Color UnavailableColor => unavailableColor;

        public Color AvailableColor => availableColor;

        public Color HintColor => hintColor;

        public GameObject MoreAbove => moreAbove;

        public GameObject MoreBelow => moreBelow;

        public int SubmenuWindowStart { get; private set; }

        public bool CursorVisible { get; private set; } = true;

        public bool Interactive { get; private set; } = true;

        public bool AssistedMode
        {
            get => assistedMode;
            set
            {
                assistedMode = value;
                Render();
            }
        }

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

        public void ConfigureScroll(GameObject above, GameObject below)
        {
            moreAbove = above;
            moreBelow = below;
        }

        public void Bind(BattleMenuController controller)
        {
            Controller = controller;
            blinkClock = 0f;
            CursorVisible = true;
            Render();
        }

        public void SetInteractive(bool interactive)
        {
            if (Interactive == interactive)
            {
                return;
            }

            Interactive = interactive;
            RestartBlink();
        }

        public void HandleNavigate(Vector2 direction)
        {
            if (Controller == null || !Interactive || Mathf.Approximately(direction.y, 0f))
            {
                return;
            }

            Controller.Navigate(direction.y > 0f ? -1 : 1);
            GameAudio.PlayUi(AudioIds.Cursor);
            RestartBlink();
        }

        public void HandleConfirm()
        {
            if (Controller == null || !Interactive)
            {
                return;
            }

            var chosen = Controller.TryConfirm(out var choice);
            RestartBlink();
            if (chosen)
            {
                GameAudio.PlayUi(AudioIds.Confirm);
                ChoiceMade?.Invoke(choice);
            }
        }

        public void HandleCancel()
        {
            if (Controller != null && Interactive && Controller.Cancel())
            {
                GameAudio.PlayUi(AudioIds.Cancel);
                RestartBlink();
            }
        }

        public void HandlePoint(BattleMenuLayer layer, int row)
        {
            if (Controller == null || !Interactive || !SameLayer(layer))
            {
                return;
            }

            Controller.Point(layer == BattleMenuLayer.Actions ? row : SubmenuWindowStart + row);
            RestartBlink();
        }

        public void HandleClick(BattleMenuLayer layer, int row)
        {
            if (Controller == null || !Interactive || !SameLayer(layer))
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
            NotifySelection();
            var inSubmenu = Controller.Layer != BattleMenuLayer.Actions;
            if (submenuPanel.activeSelf != inSubmenu)
            {
                submenuPanel.SetActive(inSubmenu);
                SubmenuToggled?.Invoke(inSubmenu);
            }

            if (inSubmenu)
            {
                RenderSubmenu();
            }
        }

        private void Update()
        {
            Advance(Time.unscaledDeltaTime);
        }

        private void NotifySelection()
        {
            var selected = Controller.Actions.Selected;
            if (ReferenceEquals(notifiedController, Controller) && notifiedEntry.HasValue &&
                notifiedEntry.Value.Action == selected.Action && notifiedEntry.Value.IsAvailable == selected.IsAvailable)
            {
                return;
            }

            notifiedController = Controller;
            notifiedEntry = selected;
            SelectionChanged?.Invoke(selected);
        }

        private bool SameLayer(BattleMenuLayer layer) =>
            layer == BattleMenuLayer.Actions ? Controller.Layer == BattleMenuLayer.Actions : Controller.Layer != BattleMenuLayer.Actions;

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
            var highlighted = Controller.HighlightedPremise(assistedMode);
            SubmenuWindowStart = WindowStart(Controller.SubmenuCursor, options.Count, submenuRows.Length);
            for (var row = 0; row < submenuRows.Length; row++)
            {
                var optionIndex = SubmenuWindowStart + row;
                var used = optionIndex < options.Count;
                submenuRows[row].gameObject.SetActive(used);
                if (used)
                {
                    submenuRows[row].text = OptionLabel(options[optionIndex]);
                    submenuRows[row].color = optionIndex == highlighted ? hintColor : availableColor;
                }
            }

            PlaceCursor(submenuCursor, submenuRows, Controller.SubmenuCursor - SubmenuWindowStart, true);
            Show(moreAbove, SubmenuWindowStart > 0);
            Show(moreBelow, SubmenuWindowStart + submenuRows.Length < options.Count);
        }

        private static void Show(GameObject indicator, bool visible)
        {
            if (indicator && indicator.activeSelf != visible)
            {
                indicator.SetActive(visible);
            }
        }

        private string OptionLabel(string option) =>
            Controller.Layer == BattleMenuLayer.Fallacies && catalog && catalog.TryGet(option, out var fallacy) ? fallacy.DisplayName : option;

        private void PlaceCursor(RectTransform cursor, IReadOnlyList<TMP_Text> rows, int index, bool layerActive)
        {
            if (!cursor)
            {
                return;
            }

            var visible = Interactive && layerActive && CursorVisible && index >= 0 && index < rows.Count;
            cursor.gameObject.SetActive(visible);
            if (visible)
            {
                var rowPosition = rows[index].rectTransform.anchoredPosition;
                cursor.anchoredPosition = new Vector2(rowPosition.x + CursorOffsetX, rowPosition.y + CursorOffsetY);
            }
        }
    }
}
