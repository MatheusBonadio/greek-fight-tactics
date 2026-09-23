using Game.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    public sealed class OptionBarPointer : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        [SerializeField] private OptionsView panel;
        [SerializeField] private int row;
        [SerializeField] private RectTransform track;

        public void Configure(OptionsView optionsPanel, int rowIndex, RectTransform barTrack)
        {
            panel = optionsPanel;
            row = rowIndex;
            track = barTrack;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Apply(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Apply(eventData);
        }

        private void Apply(PointerEventData eventData)
        {
            if (!panel || !track || eventData == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(track, eventData.position, eventData.pressEventCamera, out var local))
            {
                return;
            }

            var span = track.rect.width;
            if (span <= 0f)
            {
                return;
            }

            var ratio = Mathf.Clamp01((local.x - track.rect.xMin) / span);
            panel.SetStep(row, Mathf.RoundToInt(ratio * GameSettings.VolumeSteps));
        }
    }
}
