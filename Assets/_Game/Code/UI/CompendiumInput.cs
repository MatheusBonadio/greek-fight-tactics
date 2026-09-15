using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    public sealed class CompendiumInput : MonoBehaviour
    {
        [SerializeField] private InputActionAsset controls;
        [SerializeField] private CompendiumView compendium;

        private InputAction navigate;
        private InputAction cancel;

        public InputActionAsset Controls => controls;

        public void Configure(InputActionAsset asset, CompendiumView view)
        {
            controls = asset;
            compendium = view;
        }

        private void OnEnable()
        {
            if (!controls || !compendium)
            {
                return;
            }

            navigate = controls.FindAction(BattleMenuInput.NavigateAction, true);
            cancel = controls.FindAction(BattleMenuInput.CancelAction, true);
            navigate.performed += OnNavigate;
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
            cancel.performed -= OnCancel;
            navigate = null;
        }

        private void OnNavigate(InputAction.CallbackContext context)
        {
            compendium.HandleNavigate(context.ReadValue<Vector2>());
        }

        private void OnCancel(InputAction.CallbackContext context)
        {
            compendium.HandleCancel();
        }
    }
}
