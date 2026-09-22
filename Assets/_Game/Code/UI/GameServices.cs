using System;
using System.IO;
using System.Linq;
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
        public const string DataDirectoryVariable = "GFT_DATA_DIRECTORY";
        public const float OnlineBootBudgetSeconds = 2f;
        public const float DefaultPushRetrySeconds = 30f;

        [SerializeField] private NetworkConfigSO network;
        [SerializeField] private FallacyCatalogSO catalog;
        [SerializeField] private FallbackLibrarySO fallbackLibrary;
        [SerializeField] private string dataDirectoryOverride;

        private ArgumentValidator validator;
        private FallacyCompendium compendium;
        private SaveData sessionSave;
        private Task<bool> pushing;
        private float nextPushRetry;

        public event Action<GameSettings> SettingsChanged;

        public float PushRetrySeconds { get; set; } = DefaultPushRetrySeconds;

        public bool IsPushing => pushing is { IsCompleted: false };

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
            LoadSessionProgress(TryLoadActiveSave(out var save) ? save : null);
        }

        public bool TryLoadActiveSave(out SaveData save)
        {
            save = null;
            return LocalSaves != null && LocalSaves.Exists && LocalSaves.TryLoad(out save, out _);
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
            if (Saves == null || ActiveSlot != SaveSlots.DefaultIndex)
            {
                LocalSaves.Save(save);
                return;
            }

            Saves.SaveLocal(save);
            sessionSave = save;
            PushSessionSave();
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

            if (IsBooted && Time.unscaledTime >= nextPushRetry)
            {
                PushSessionSave();
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
            LoadSessionProgress(TryLoadActiveSave(out var activeSave) ? activeSave : null);
            SettingsStore = new LocalSettingsStore(DataDirectory);
            Settings = SettingsStore.Load();
            ApplySettings();

            var backend = OnlineBackendOverride.Current ?? OnlineBackend.FromConfig(network);
            if (backend == null)
            {
                return;
            }

            Auth = new FirebaseAnonymousAuth(backend.Transport, backend.Endpoints, new FileAuthSessionStore(DataDirectory));
            Saves = new SaveSynchronizer(SlotCatalog.StoreFor(SaveSlots.DefaultIndex), new FirestoreSaveClient(backend.Transport, Auth, backend.Firestore), backend.Delay);
            if (catalog)
            {
                Arguments = new ArgumentServiceClient(backend.Transport, Auth, backend.Endpoints.ArgumentUrl, Validator(), backend.Delay);
            }

            await ConnectWithinBudgetAsync(cancellation);
        }

        private async Task ConnectWithinBudgetAsync(CancellationToken cancellation)
        {
            using var online = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            var connecting = ConnectAsync(online.Token);
            var deadline = Time.realtimeSinceStartup + OnlineBootBudgetSeconds;
            while (!connecting.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                await Awaitable.NextFrameAsync(cancellation);
            }

            online.Cancel();
            await connecting;
        }

        private async Task ConnectAsync(CancellationToken cancellation)
        {
            try
            {
                await Auth.GetIdTokenAsync(false, cancellation);
                await Saves.ReconcileAsync(cancellation);
                if (TryLoadActiveSave(out var reconciled))
                {
                    Compendium.UnlockAll(reconciled.Compendium);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Online services unavailable, continuing offline: {exception.Message}");
            }
        }

        private void PushSessionSave()
        {
            nextPushRetry = Time.unscaledTime + PushRetrySeconds;
            if (IsPushing || sessionSave == null || Saves is not { HasPendingPush: true } || !ReferenceEquals(Saves.Current, sessionSave))
            {
                return;
            }

            pushing = Saves.PushPendingAsync(destroyCancellationToken);
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
        }

        private ArgumentValidator Validator() => validator ??= new ArgumentValidator(catalog.RefutableIds);
    }
}
