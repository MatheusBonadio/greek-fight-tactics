using System;
using System.Collections.Generic;
using Game.Content;
using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    public sealed class OptionsView : MonoBehaviour, IMenuPanel
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text[] labels = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] values = Array.Empty<TMP_Text>();
        [SerializeField] private RectTransform cursor;
        [SerializeField] private UiTextSO text;

        public event Action Closed;

        public event Action<GameSettings> SettingsChanged;

        public OptionsModel Model { get; private set; }

        public IReadOnlyList<TMP_Text> Labels => labels;

        public IReadOnlyList<TMP_Text> Values => values;

        public void Configure(TMP_Text titleLabel, TMP_Text[] rowLabels, TMP_Text[] rowValues, RectTransform rowCursor, UiTextSO uiText)
        {
            title = titleLabel;
            labels = rowLabels;
            values = rowValues;
            cursor = rowCursor;
            text = uiText;
        }

        public void Bind(OptionsModel model)
        {
            if (Model != null)
            {
                Model.Changed -= OnChanged;
            }

            Model = model;
            if (Model != null)
            {
                Model.Changed += OnChanged;
            }

            Render();
        }

        public void HandleNavigate(Vector2 direction)
        {
            if (Model == null)
            {
                return;
            }

            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                Model.Adjust(direction.x > 0f ? 1 : -1);
            }
            else if (!Mathf.Approximately(direction.y, 0f))
            {
                Model.Move(direction.y > 0f ? -1 : 1);
                Render();
            }
        }

        public void HandleConfirm()
        {
            if (Model == null)
            {
                return;
            }

            if (Model.Confirm())
            {
                Closed?.Invoke();
            }
        }

        public void HandleCancel()
        {
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

            if (title)
            {
                title.text = text.Get(OptionsModel.TitleKey);
            }

            for (var row = 0; row < labels.Length && row < OptionsModel.RowCount; row++)
            {
                var option = (OptionRow)row;
                labels[row].text = text.Get(OptionsModel.LabelKey(option));
                values[row].text = ValueText(option);
            }

            UiCursor.Place(cursor, Model.Cursor < labels.Length ? labels[Model.Cursor] : null, true);
        }

        private string ValueText(OptionRow row) => row switch
        {
            OptionRow.Volume => $"{Model.VolumePercent}%",
            OptionRow.TextSpeed => text.Get(OptionsModel.TextSpeedKey(Model.Settings.TextSpeed)),
            OptionRow.AssistedMode => text.Get(Model.Settings.AssistedMode ? OptionsModel.OnKey : OptionsModel.OffKey),
            _ => string.Empty
        };

        private void OnChanged()
        {
            Render();
            SettingsChanged?.Invoke(Model.Settings);
        }
    }
}
