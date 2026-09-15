using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    public sealed class SystemMenuInput : MonoBehaviour
    {
        [SerializeField] private InputActionAsset controls;

        private InputAction navigate;
        private InputAction confirm;
        private InputAction cancel;

        public IMenuPanel Panel { get; set; }

        public InputActionAsset Controls => controls;

        public void Configure(InputActionAsset asset)
        {
            controls = asset;
        }

        private void OnEnable()
        {
            if (!controls)
            {
                return;
            }

            navigate = controls.FindAction(BattleMenuInput.NavigateAction, true);
            confirm = controls.FindAction(BattleMenuInput.ConfirmAction, true);
            cancel = controls.FindAction(BattleMenuInput.CancelAction, true);
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
            Panel?.HandleNavigate(context.ReadValue<Vector2>());
        }

        private void OnConfirm(InputAction.CallbackContext context)
        {
            if (context.control?.device is not Mouse)
            {
                Panel?.HandleConfirm();
            }
        }

        private void OnCancel(InputAction.CallbackContext context)
        {
            Panel?.HandleCancel();
        }
    }
}
