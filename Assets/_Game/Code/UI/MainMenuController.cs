using Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI
{
    public sealed class MainMenuController : MonoBehaviour
    {
        public const string DefaultGameplayScene = "GridSandbox";

        [SerializeField] private GameServices services;
        [SerializeField] private SystemMenuInput input;
        [SerializeField] private MenuListView mainMenu;
        [SerializeField] private OptionsView options;
        [SerializeField] private CreditsView credits;
        [SerializeField] private SaveSlotsView slots;
        [SerializeField] private string gameplayScene = DefaultGameplayScene;

        public bool IsReady { get; private set; }

        public GameServices Services => services;

        public SystemMenuInput Input => input;

        public MenuListView MainMenu => mainMenu;

        public OptionsView Options => options;

        public CreditsView Credits => credits;

        public SaveSlotsView Slots => slots;

        public string GameplayScene => gameplayScene;

        public void Configure(GameServices gameServices, SystemMenuInput menuInput, MenuListView main, OptionsView optionsView, CreditsView creditsView, SaveSlotsView slotsView, string sceneName)
        {
            services = gameServices;
            input = menuInput;
            mainMenu = main;
            options = optionsView;
            credits = creditsView;
            slots = slotsView;
            gameplayScene = sceneName;
        }

        public void ShowMain()
        {
            options.gameObject.SetActive(false);
            credits.gameObject.SetActive(false);
            slots.gameObject.SetActive(false);
            mainMenu.gameObject.SetActive(true);
            mainMenu.Bind(SystemMenus.Main(services.HasContinue));
            input.Panel = mainMenu;
        }

        private void OnEnable()
        {
            mainMenu.Chosen += OnChosen;
            options.Closed += ShowMain;
            options.SettingsChanged += OnSettingsChanged;
            credits.Closed += ShowMain;
            slots.Closed += ShowMain;
            slots.SlotChosen += OnSlotChosen;
        }

        private void OnDisable()
        {
            mainMenu.Chosen -= OnChosen;
            options.Closed -= ShowMain;
            options.SettingsChanged -= OnSettingsChanged;
            credits.Closed -= ShowMain;
            slots.Closed -= ShowMain;
            slots.SlotChosen -= OnSlotChosen;
        }

        private void Update()
        {
            if (IsReady || !services.IsBooted)
            {
                return;
            }

            IsReady = true;
            ShowMain();
        }

        private void OnChosen(string id)
        {
            switch (id)
            {
                case SystemMenus.Continue:
                    ShowSlots(SaveSlotsMode.Load);
                    break;
                case SystemMenus.NewGame:
                    ShowSlots(SaveSlotsMode.Save);
                    break;
                case SystemMenus.Options:
                    mainMenu.gameObject.SetActive(false);
                    options.gameObject.SetActive(true);
                    options.Bind(new OptionsModel(services.Settings.Clone()));
                    input.Panel = options;
                    break;
                case SystemMenus.Credits:
                    mainMenu.gameObject.SetActive(false);
                    credits.Show();
                    input.Panel = credits;
                    break;
                case SystemMenus.Quit:
                    Application.Quit();
                    break;
            }
        }

        private void ShowSlots(SaveSlotsMode mode)
        {
            mainMenu.gameObject.SetActive(false);
            slots.gameObject.SetActive(true);
            slots.Bind(new SaveSlotsModel(mode, services.SlotCatalog.ReadAll()));
            input.Panel = slots;
        }

        private void OnSlotChosen(int index)
        {
            if (slots.Model.Mode == SaveSlotsMode.Load)
            {
                services.SelectSlot(index);
            }
            else
            {
                services.StartNewGame(index);
            }

            SceneManager.LoadScene(gameplayScene);
        }

        private void OnSettingsChanged(GameSettings settings)
        {
            services.UpdateSettings(settings.Clone());
        }
    }
}
