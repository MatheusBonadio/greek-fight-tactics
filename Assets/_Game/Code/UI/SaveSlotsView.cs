using System;
using System.Collections.Generic;
using Game.Content;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    public sealed class SaveSlotsView : MonoBehaviour, IMenuPanel
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text[] rows = Array.Empty<TMP_Text>();
        [SerializeField] private RectTransform cursor;
        [SerializeField] private GameObject confirmPanel;
        [SerializeField] private TMP_Text confirmLabel;
        [SerializeField] private TMP_Text[] confirmRows = Array.Empty<TMP_Text>();
        [SerializeField] private RectTransform confirmCursor;
        [SerializeField] private UiTextSO text;
        [SerializeField] private Color availableColor = new Color32(248, 248, 240, 255);
        [SerializeField] private Color unavailableColor = new Color32(120, 120, 128, 255);

        public event Action<int> SlotChosen;

        public event Action Closed;

        public SaveSlotsModel Model { get; private set; }

        public TMP_Text Title => title;

        public IReadOnlyList<TMP_Text> Rows => rows;

        public GameObject ConfirmPanel => confirmPanel;

        public TMP_Text ConfirmLabel => confirmLabel;

        public IReadOnlyList<TMP_Text> ConfirmRows => confirmRows;

        public Color UnavailableColor => unavailableColor;

        public void Configure(
            TMP_Text titleLabel,
            TMP_Text[] slotRows,
            RectTransform slotCursor,
            GameObject confirmation,
            TMP_Text confirmationLabel,
            TMP_Text[] confirmationRows,
            RectTransform confirmationCursor,
            UiTextSO uiText)
        {
            title = titleLabel;
            rows = slotRows;
            cursor = slotCursor;
            confirmPanel = confirmation;
            confirmLabel = confirmationLabel;
            confirmRows = confirmationRows;
            confirmCursor = confirmationCursor;
            text = uiText;
        }

        public void Bind(SaveSlotsModel model)
        {
            Model = model;
            Render();
        }

        public void HandleNavigate(Vector2 direction)
        {
            if (Model == null || Mathf.Approximately(direction.y, 0f))
            {
                return;
            }

            Model.Move(direction.y > 0f ? -1 : 1);
            Render();
        }

        public void HandleConfirm()
        {
            if (Model == null)
            {
                return;
            }

            var chosen = Model.TryConfirm(out var slot);
            Render();
            if (chosen)
            {
                SlotChosen?.Invoke(slot);
            }
        }

        public void HandleCancel()
        {
            if (Model != null && Model.Cancel())
            {
                Render();
                return;
            }

            Closed?.Invoke();
        }

        public void HandlePoint(int row)
        {
            if (Model == null)
            {
                return;
            }

            Model.Point(row);
            Render();
        }

        public void Render()
        {
            if (Model == null)
            {
                return;
            }

            title.text = text.Get(Model.Mode == SaveSlotsMode.Save ? SaveSlotText.TitleSaveKey : SaveSlotText.TitleLoadKey);
            for (var index = 0; index < rows.Length; index++)
            {
                var used = index < Model.Slots.Count;
                rows[index].gameObject.SetActive(used);
                if (used)
                {
                    rows[index].text = SaveSlotText.Line(Model.Slots[index], text);
                    rows[index].color = Model.IsEnabled(index) ? availableColor : unavailableColor;
                }
            }

            var confirming = Model.Step == SaveSlotsStep.ConfirmingOverwrite;
            UiCursor.Place(cursor, Model.Cursor < rows.Length ? rows[Model.Cursor] : null, !confirming);
            confirmPanel.SetActive(confirming);
            if (!confirming)
            {
                return;
            }

            confirmLabel.text = text.Get(SaveSlotText.ConfirmOverwriteKey);
            confirmRows[SaveSlotsModel.ConfirmYes].text = text.Get(SaveSlotText.YesKey);
            confirmRows[SaveSlotsModel.ConfirmNo].text = text.Get(SaveSlotText.NoKey);
            UiCursor.Place(confirmCursor, confirmRows[Model.ConfirmCursor], true);
        }
    }
}
