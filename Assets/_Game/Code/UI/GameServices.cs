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

        public ArgumentDiskCache ArgumentCache { get; private set; }

        public LocalSaveStore LocalSaves { get; private set; }

        public FirebaseAnonymousAuth Auth { get; private set; }

        public ArgumentServiceClient Arguments { get; private set; }

        public SaveSynchronizer Saves { get; private set; }

        public Task BootTask { get; private set; }

        public bool IsBooted => BootTask is { IsCompletedSuccessfully: true };

        public string DataDirectory => string.IsNullOrWhiteSpace(dataDirectoryOverride) ? Application.persistentDataPath : dataDirectoryOverride;

        public void Configure(NetworkConfigSO networkConfig, FallacyCatalogSO fallacyCatalog, FallbackLibrarySO fallbacks, string dataDirectory)
        {
            network = networkConfig;
            catalog = fallacyCatalog;
            fallbackLibrary = fallbacks;
            dataDirectoryOverride = dataDirectory;
        }

        public Task SaveArgumentCache() => ArgumentCache?.SaveAsync() ?? Task.CompletedTask;

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
            LocalSaves = new LocalSaveStore(Path.Combine(DataDirectory, SaveFolder));

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
            Saves = new SaveSynchronizer(LocalSaves, new FirestoreSaveClient(transport, Auth, firestore));
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

        private ArgumentValidator Validator() => validator ??= new ArgumentValidator(catalog.RefutableIds);
    }
}
