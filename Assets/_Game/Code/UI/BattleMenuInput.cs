using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    public sealed class BattleMenuInput : MonoBehaviour
    {
        public const string NavigateAction = "Grid/Navigate";
        public const string ConfirmAction = "Grid/Confirm";
        public const string CancelAction = "Grid/Cancel";

        [SerializeField] private InputActionAsset controls;
        [SerializeField] private ActionMenuView menu;

        private InputAction navigate;
        private InputAction confirm;
        private InputAction cancel;

        public InputActionAsset Controls => controls;

        public void Configure(InputActionAsset asset, ActionMenuView view)
        {
            controls = asset;
            menu = view;
        }

        private void OnEnable()
        {
            if (!controls || !menu)
            {
                return;
            }

            navigate = controls.FindAction(NavigateAction, true);
            confirm = controls.FindAction(ConfirmAction, true);
            cancel = controls.FindAction(CancelAction, true);
            navigate.performed += OnNavigate;
            confirm.performed += OnConfirm;
            cancel.performed += OnCancel;
            controls.Enable();
        }

        private void OnDisable()
        {
            if (navigate == null)
            {
                return;
            }

            navigate.performed -= OnNavigate;
            confirm.performed -= OnConfirm;
            cancel.performed -= OnCancel;
            navigate = null;
        }

        private void OnNavigate(InputAction.CallbackContext context)
        {
            menu.HandleNavigate(context.ReadValue<Vector2>());
        }

        private void OnConfirm(InputAction.CallbackContext context)
        {
            if (context.control?.device is not Mouse)
            {
                menu.HandleConfirm();
            }
        }

        private void OnCancel(InputAction.CallbackContext context)
        {
            menu.HandleCancel();
        }
    }
}
