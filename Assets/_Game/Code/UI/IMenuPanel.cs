using UnityEngine;

namespace Game.UI
{
    public interface IMenuPanel
    {
        void HandleNavigate(Vector2 direction);

        void HandleConfirm();

        void HandleCancel();

        void HandlePoint(int row);
    }
}
