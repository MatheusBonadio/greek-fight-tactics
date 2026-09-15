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
        [SerializeField] private string gameplayScene = DefaultGameplayScene;

        public bool IsReady { get; private set; }

        public GameServices Services => services;

        public SystemMenuInput Input => input;

        public MenuListView MainMenu => mainMenu;

        public OptionsView Options => options;

        public CreditsView Credits => credits;

        public string GameplayScene => gameplayScene;

        public void Configure(GameServices gameServices, SystemMenuInput menuInput, MenuListView main, OptionsView optionsView, CreditsView creditsView, string sceneName)
        {
            services = gameServices;
            input = menuInput;
            mainMenu = main;
            options = optionsView;
            credits = creditsView;
            gameplayScene = sceneName;
        }

        public void ShowMain()
        {
            options.gameObject.SetActive(false);
            credits.gameObject.SetActive(false);
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
        }

        private void OnDisable()
        {
            mainMenu.Chosen -= OnChosen;
            options.Closed -= ShowMain;
            options.SettingsChanged -= OnSettingsChanged;
            credits.Closed -= ShowMain;
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
                case SystemMenus.NewGame:
                    SceneManager.LoadScene(gameplayScene);
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

        private void OnSettingsChanged(GameSettings settings)
        {
            services.UpdateSettings(settings.Clone());
        }
    }
}
