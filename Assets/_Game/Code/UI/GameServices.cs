using System;
using System.Linq;
using System.Threading.Tasks;
using Game.Content;
using Game.Core;
using Game.Net;
using UnityEngine;

namespace Game.UI
{
    public sealed class GameServices : MonoBehaviour
    {
        public const string DataDirectoryVariable = "GFT_DATA_DIRECTORY";

        [SerializeField] private FallacyCatalogSO catalog;
        [SerializeField] private AudioLibrarySO audioLibrary;
        [SerializeField] private string dataDirectoryOverride;

        private FallacyCompendium compendium;

        public event Action<GameSettings> SettingsChanged;

        public SaveDatabase Database { get; private set; }

        public SaveSlotCatalog SlotCatalog { get; private set; }

        public int ActiveSlot { get; private set; } = SaveSlots.DefaultIndex;

        public LocalSaveStore LocalSaves { get; private set; }

        public LocalSettingsStore SettingsStore { get; private set; }

        public GameSettings Settings { get; private set; } = new();

        public float Playtime { get; private set; }

        public long PlaytimeSeconds => (long)Playtime;

        public Task BootTask { get; private set; }

        public bool IsBooted => BootTask is { IsCompletedSuccessfully: true };

        public bool HasContinue => SlotCatalog != null && SlotCatalog.AnyUsable();

        public FallacyCatalogSO Catalog => catalog;

        public FallacyCompendium Compendium => compendium ??= new FallacyCompendium(catalog ? catalog.All.Select(fallacy => fallacy.Id) : Array.Empty<string>());

        public string DataDirectory
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(dataDirectoryOverride))
                {
                    return dataDirectoryOverride;
                }

                var fromEnvironment = Environment.GetEnvironmentVariable(DataDirectoryVariable);
                return string.IsNullOrWhiteSpace(fromEnvironment) ? Application.persistentDataPath : fromEnvironment;
            }
        }

        public void Configure(FallacyCatalogSO fallacyCatalog, string dataDirectory)
        {
            catalog = fallacyCatalog;
            dataDirectoryOverride = dataDirectory;
        }

        public void ConfigureAudio(AudioLibrarySO library)
        {
            audioLibrary = library;
        }

        public void UpdateSettings(GameSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            SettingsStore?.Save(settings);
            ApplySettings();
            SettingsChanged?.Invoke(settings);
        }

        public void SelectSlot(int index)
        {
            RequireBoot();
            SlotCatalog.WriteActiveSlot(index);
            ActiveSlot = index;
            LocalSaves = SlotCatalog.StoreFor(index);
            LoadSessionProgress(TryLoadActiveSave(out var save) ? save : null);
        }

        public bool TryLoadActiveSave(out SaveData save)
        {
            save = null;
            return LocalSaves != null && LocalSaves.TryLoad(out save, out _);
        }

        public void StartNewGame(int index)
        {
            SelectSlot(index);
            var save = new SaveData { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
            LoadSessionProgress(save);
            Autosave(save);
        }

        public void SaveToSlot(int index, SaveData save)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            SelectSlot(index);
            LoadSessionProgress(save);
            Autosave(save);
        }

        public void Autosave(SaveData save)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            RequireBoot();
            LocalSaves.Save(save);
        }

        private void Start()
        {
            BootTask = Boot();
        }

        private void Update()
        {
            if (IsBooted && Time.timeScale > 0f)
            {
                Playtime += Time.unscaledDeltaTime;
            }
        }

        private Task Boot()
        {
            try
            {
                Database = new SaveDatabase(DataDirectory);
                SlotCatalog = new SaveSlotCatalog(Database);
                ActiveSlot = SlotCatalog.ReadActiveSlot();
                LocalSaves = SlotCatalog.StoreFor(ActiveSlot);
                LoadSessionProgress(TryLoadActiveSave(out var activeSave) ? activeSave : null);
                SettingsStore = new LocalSettingsStore(Database);
                Settings = SettingsStore.Load();
                ApplySettings();
                return Task.CompletedTask;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return Task.FromException(exception);
            }
        }

        private void RequireBoot()
        {
            if (SlotCatalog == null || LocalSaves == null)
            {
                throw new InvalidOperationException("GameServices must finish booting before touching saves.");
            }
        }

        private void LoadSessionProgress(SaveData save)
        {
            Playtime = save?.PlaytimeSeconds ?? 0f;
            Compendium.Restore(save?.Compendium);
        }

        private void ApplySettings()
        {
            AudioListener.volume = Settings.MasterVolume;
            var audio = EnsureAudio();
            if (audio == null)
            {
                return;
            }

            audio.SetVolume(AudioBus.Music, Settings.MusicVolume);
            audio.SetVolume(AudioBus.Sfx, Settings.EffectsVolume);
            audio.SetVolume(AudioBus.Ui, Settings.EffectsVolume);
        }

        private GameAudio EnsureAudio()
        {
            if (audioLibrary == null)
            {
                return null;
            }

            var audio = GameAudio.Active;
            if (audio == null)
            {
                audio = new GameObject(GameAudio.ObjectName).AddComponent<GameAudio>();
            }

            audio.Configure(audioLibrary);
            return audio;
        }
    }
}
