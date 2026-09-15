using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Game.Content;
using Game.Core;
using Game.Net;
using UnityEngine;

namespace Game.UI
{
    public sealed class GameServices : MonoBehaviour
    {
        public const string ArgumentCacheFolder = "argument_cache";
        public const string SaveFolder = "saves";

        [SerializeField] private NetworkConfigSO network;
        [SerializeField] private FallacyCatalogSO catalog;
        [SerializeField] private FallbackLibrarySO fallbackLibrary;
        [SerializeField] private string dataDirectoryOverride;

        private ArgumentValidator validator;

        public event Action<GameSettings> SettingsChanged;

        public ArgumentDiskCache ArgumentCache { get; private set; }

        public SaveSlotCatalog SlotCatalog { get; private set; }

        public int ActiveSlot { get; private set; } = SaveSlots.DefaultIndex;

        public LocalSaveStore LocalSaves { get; private set; }

        public LocalSettingsStore SettingsStore { get; private set; }

        public GameSettings Settings { get; private set; } = new();

        public float Playtime { get; private set; }

        public long PlaytimeSeconds => (long)Playtime;

        public FirebaseAnonymousAuth Auth { get; private set; }

        public ArgumentServiceClient Arguments { get; private set; }

        public SaveSynchronizer Saves { get; private set; }

        public Task BootTask { get; private set; }

        public bool IsBooted => BootTask is { IsCompletedSuccessfully: true };

        public bool HasContinue => SlotCatalog != null && SlotCatalog.AnyUsable();

        public FallacyCatalogSO Catalog => catalog;

        public string DataDirectory => string.IsNullOrWhiteSpace(dataDirectoryOverride) ? Application.persistentDataPath : dataDirectoryOverride;

        public void Configure(NetworkConfigSO networkConfig, FallacyCatalogSO fallacyCatalog, FallbackLibrarySO fallbacks, string dataDirectory)
        {
            network = networkConfig;
            catalog = fallacyCatalog;
            fallbackLibrary = fallbacks;
            dataDirectoryOverride = dataDirectory;
        }

        public Task SaveArgumentCache() => ArgumentCache?.SaveAsync() ?? Task.CompletedTask;

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
            Playtime = TryLoadActiveSave(out var save) ? save.PlaytimeSeconds : 0f;
        }

        public bool TryLoadActiveSave(out SaveData save)
        {
            save = null;
            return LocalSaves != null && LocalSaves.Exists && LocalSaves.TryLoad(out save, out _);
        }

        public void StartNewGame(int index)
        {
            SelectSlot(index);
            Playtime = 0f;
            Autosave(new SaveData { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        }

        public void SaveToSlot(int index, SaveData save)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            SelectSlot(index);
            Playtime = save.PlaytimeSeconds;
            Autosave(save);
        }

        public void Autosave(SaveData save)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            RequireBoot();
            if (Saves == null || ActiveSlot != SaveSlots.DefaultIndex)
            {
                LocalSaves.Save(save);
                return;
            }

            Saves.SaveLocal(save);
            _ = Saves.PushPendingAsync(destroyCancellationToken);
        }

        public PrewarmedArgumentSource CreateArgumentSource(int seed)
        {
            if (!catalog || !fallbackLibrary || ArgumentCache == null)
            {
                throw new InvalidOperationException("GameServices needs a fallacy catalog, a fallback library and a finished boot.");
            }

            return new PrewarmedArgumentSource(Arguments, ArgumentCache, Validator(), new FallbackArgumentLibrary(fallbackLibrary, Validator(), seed), seed);
        }

        public async Task<int> PrewarmAsync(PrewarmedArgumentSource source, int debaterId, int count)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var added = await source.PrewarmAsync(debaterId, count, destroyCancellationToken);
            await SaveArgumentCache();
            return added;
        }

        private void Start()
        {
            BootTask = BootAsync(destroyCancellationToken);
        }

        private void Update()
        {
            if (IsBooted && Time.timeScale > 0f)
            {
                Playtime += Time.unscaledDeltaTime;
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                _ = SaveArgumentCache();
            }
        }

        private void OnApplicationQuit()
        {
            _ = SaveArgumentCache();
        }

        private void OnDestroy()
        {
            _ = SaveArgumentCache();
        }

        private async Task BootAsync(CancellationToken cancellation)
        {
            ArgumentCache = new ArgumentDiskCache(Path.Combine(DataDirectory, ArgumentCacheFolder));
            ArgumentCache.Load();
            SlotCatalog = new SaveSlotCatalog(Path.Combine(DataDirectory, SaveFolder));
            ActiveSlot = SlotCatalog.ReadActiveSlot();
            LocalSaves = SlotCatalog.StoreFor(ActiveSlot);
            Playtime = TryLoadActiveSave(out var activeSave) ? activeSave.PlaytimeSeconds : 0f;
            SettingsStore = new LocalSettingsStore(DataDirectory);
            Settings = SettingsStore.Load();
            ApplySettings();

            if (!network || !network.IsConfigured)
            {
                return;
            }

            var transport = new UnityWebRequestTransport();
            var endpoints = network.UseEmulator
                ? FirebaseEndpoints.Emulator(network.ProjectId, network.EmulatorHost, network.Region)
                : FirebaseEndpoints.Production(network.WebApiKey, network.ProjectId, network.Region);
            var firestore = network.UseEmulator
                ? FirestoreEndpoints.Emulator(network.ProjectId, network.EmulatorHost)
                : FirestoreEndpoints.Production(network.ProjectId);

            Auth = new FirebaseAnonymousAuth(transport, endpoints, new FileAuthSessionStore(DataDirectory));
            Saves = new SaveSynchronizer(SlotCatalog.StoreFor(SaveSlots.DefaultIndex), new FirestoreSaveClient(transport, Auth, firestore));
            if (catalog)
            {
                Arguments = new ArgumentServiceClient(transport, Auth, endpoints.ArgumentUrl, Validator());
            }

            try
            {
                await Auth.GetIdTokenAsync(false, cancellation);
                await Saves.ReconcileAsync(cancellation);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Online services unavailable, continuing offline: {exception.Message}");
            }
        }

        private void RequireBoot()
        {
            if (SlotCatalog == null || LocalSaves == null)
            {
                throw new InvalidOperationException("GameServices must finish booting before touching saves.");
            }
        }

        private void ApplySettings()
        {
            AudioListener.volume = Settings.MasterVolume;
        }

        private ArgumentValidator Validator() => validator ??= new ArgumentValidator(catalog.RefutableIds);
    }
}
