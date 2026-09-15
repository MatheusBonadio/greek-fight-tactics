using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    public sealed class MenuRowPointer : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [SerializeField] private ActionMenuView menu;
        [SerializeField] private BattleMenuLayer layer;
        [SerializeField] private int row;

        public void Configure(ActionMenuView view, BattleMenuLayer rowLayer, int rowIndex)
        {
            menu = view;
            layer = rowLayer;
            row = rowIndex;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            menu.HandlePoint(layer, row);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                menu.HandleClick(layer, row);
            }
        }
    }
}
