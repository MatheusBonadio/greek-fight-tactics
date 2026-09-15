using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    public sealed class PanelRowPointer : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [SerializeField] private MonoBehaviour panel;
        [SerializeField] private int row;

        public void Configure(MonoBehaviour menuPanel, int rowIndex)
        {
            panel = menuPanel;
            row = rowIndex;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            (panel as IMenuPanel)?.HandlePoint(row);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || panel is not IMenuPanel target)
            {
                return;
            }

            target.HandlePoint(row);
            target.HandleConfirm();
        }
    }
}
