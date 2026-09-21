using System;
using Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI
{
    public sealed class PauseMenuController : MonoBehaviour, IMenuPanel
    {
        public const string DefaultMainMenuScene = "MainMenu";

        [SerializeField] private GameServices services;
        [SerializeField] private SystemMenuInput input;
        [SerializeField] private GameObject panel;
        [SerializeField] private MenuListView pauseMenu;
        [SerializeField] private OptionsView options;
        [SerializeField] private SaveSlotsView slots;
        [SerializeField] private CompendiumView compendium;
        [SerializeField] private string mainMenuScene = DefaultMainMenuScene;

        private float resumeTimeScale = 1f;
        private int resumedFrame = -1;

        public GameStateMachine StateMachine { get; set; }

        public Func<SaveData> SaveProvider { get; set; }

        public bool IsPaused { get; private set; }

        public bool CapturesInput => IsPaused || resumedFrame == Time.frameCount;

        public GameServices Services => services;

        public SystemMenuInput Input => input;

        public GameObject Panel => panel;

        public MenuListView PauseMenu => pauseMenu;

        public OptionsView Options => options;

        public SaveSlotsView Slots => slots;

        public CompendiumView Compendium => compendium;

        public bool CanSave => SaveProvider != null && services && services.IsBooted;

        public void Configure(GameServices gameServices, SystemMenuInput menuInput, GameObject pausePanel, MenuListView menu, OptionsView optionsView, SaveSlotsView slotsView, CompendiumView compendiumView, string sceneName)
        {
            services = gameServices;
            input = menuInput;
            panel = pausePanel;
            pauseMenu = menu;
            options = optionsView;
            slots = slotsView;
            compendium = compendiumView;
            mainMenuScene = sceneName;
        }

        public void BindCompendium(CompendiumView view, FallacyCompendium progress)
        {
            if (compendium)
            {
                compendium.Closed -= ShowPauseList;
            }

            compendium = view;
            if (compendium)
            {
                compendium.Closed += ShowPauseList;
                compendium.Bind(progress);
                compendium.gameObject.SetActive(false);
            }
        }

        public void Pause()
        {
            if (IsPaused || (StateMachine != null && !StateMachine.TryOpenOverlay(GameMode.Paused)))
            {
                return;
            }

            IsPaused = true;
            resumeTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            panel.SetActive(true);
            ShowPauseList();
        }

        public void Resume()
        {
            if (!IsPaused)
            {
                return;
            }

            StateMachine?.TryCloseOverlay();
            IsPaused = false;
            resumedFrame = Time.frameCount;
            Time.timeScale = resumeTimeScale;
            panel.SetActive(false);
            input.Panel = this;
        }

        public void HandleNavigate(Vector2 direction)
        {
        }

        public void HandleConfirm()
        {
        }

        public void HandleCancel()
        {
            Pause();
        }

        public void HandlePoint(int row)
        {
        }

        private void Awake()
        {
            if (!services)
            {
                services = FindAnyObjectByType<GameServices>();
            }
        }

        private void OnEnable()
        {
            pauseMenu.Chosen += OnChosen;
            pauseMenu.Cancelled += Resume;
            options.Closed += ShowPauseList;
            options.SettingsChanged += OnSettingsChanged;
            slots.Closed += ShowPauseList;
            slots.SlotChosen += OnSlotChosen;
            if (compendium)
            {
                compendium.Closed += ShowPauseList;
            }
        }

        private void OnDisable()
        {
            pauseMenu.Chosen -= OnChosen;
            pauseMenu.Cancelled -= Resume;
            options.Closed -= ShowPauseList;
            options.SettingsChanged -= OnSettingsChanged;
            slots.Closed -= ShowPauseList;
            slots.SlotChosen -= OnSlotChosen;
            if (compendium)
            {
                compendium.Closed -= ShowPauseList;
            }

            if (IsPaused)
            {
                Time.timeScale = resumeTimeScale;
                IsPaused = false;
            }
        }

        private void Start()
        {
            panel.SetActive(false);
            input.Panel = this;
            if (compendium && services)
            {
                BindCompendium(compendium, services.Compendium);
            }
        }

        private void ShowPauseList()
        {
            options.gameObject.SetActive(false);
            slots.gameObject.SetActive(false);
            if (compendium)
            {
                compendium.gameObject.SetActive(false);
            }

            pauseMenu.gameObject.SetActive(true);
            var model = SystemMenus.Pause();
            model.SetEnabled(SystemMenus.Save, CanSave);
            model.SetEnabled(SystemMenus.Compendium, compendium && compendium.Compendium != null);
            pauseMenu.Bind(model);
            input.Panel = pauseMenu;
        }

        private void OnChosen(string id)
        {
            switch (id)
            {
                case SystemMenus.Resume:
                    Resume();
                    break;
                case SystemMenus.Save:
                    pauseMenu.gameObject.SetActive(false);
                    slots.gameObject.SetActive(true);
                    slots.Bind(new SaveSlotsModel(SaveSlotsMode.Save, services.SlotCatalog.ReadAll()));
                    input.Panel = slots;
                    break;
                case SystemMenus.Options:
                    pauseMenu.gameObject.SetActive(false);
                    options.gameObject.SetActive(true);
                    options.Bind(new OptionsModel((services ? services.Settings : new GameSettings()).Clone()));
                    input.Panel = options;
                    break;
                case SystemMenus.Compendium:
                    pauseMenu.gameObject.SetActive(false);
                    compendium.gameObject.SetActive(true);
                    compendium.Render();
                    input.Panel = compendium;
                    break;
                case SystemMenus.MainMenu:
                    ReturnToMainMenu();
                    break;
            }
        }

        private void OnSlotChosen(int index)
        {
            if (CanSave)
            {
                services.SaveToSlot(index, SaveProvider());
            }

            ShowPauseList();
        }

        private void ReturnToMainMenu()
        {
            if (CanSave)
            {
                services.Autosave(SaveProvider());
            }

            Resume();
            SceneManager.LoadScene(mainMenuScene);
        }

        private void OnSettingsChanged(GameSettings settings)
        {
            if (services)
            {
                services.UpdateSettings(settings.Clone());
            }
        }
    }
}
