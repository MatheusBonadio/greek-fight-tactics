using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Content;
using Game.Net;
using Unity.Scripting.LifecycleManagement;

namespace Game.UI
{
    public sealed class OnlineBackend
    {
        public OnlineBackend(IHttpTransport transport, FirebaseEndpoints endpoints, FirestoreEndpoints firestore, Func<TimeSpan, CancellationToken, Task> delay = null)
        {
            Transport = transport ?? throw new ArgumentNullException(nameof(transport));
            Endpoints = endpoints ?? throw new ArgumentNullException(nameof(endpoints));
            Firestore = firestore ?? throw new ArgumentNullException(nameof(firestore));
            Delay = delay;
        }

        public IHttpTransport Transport { get; }

        public FirebaseEndpoints Endpoints { get; }

        public FirestoreEndpoints Firestore { get; }

        public Func<TimeSpan, CancellationToken, Task> Delay { get; }

        public static OnlineBackend FromConfig(NetworkConfigSO network)
        {
            if (!network || !network.IsConfigured)
            {
                return null;
            }

            var endpoints = network.UseEmulator
                ? FirebaseEndpoints.Emulator(network.ProjectId, network.EmulatorHost, network.Region)
                : FirebaseEndpoints.Production(network.WebApiKey, network.ProjectId, network.Region);
            var firestore = network.UseEmulator
                ? FirestoreEndpoints.Emulator(network.ProjectId, network.EmulatorHost)
                : FirestoreEndpoints.Production(network.ProjectId);
            return new OnlineBackend(new UnityWebRequestTransport(), endpoints, firestore);
        }
    }

    public static partial class OnlineBackendOverride
    {
        [AutoStaticsCleanup]
        private static OnlineBackend backend;

        public static OnlineBackend Current => backend;

        public static void Set(OnlineBackend replacement)
        {
            backend = replacement ?? throw new ArgumentNullException(nameof(replacement));
        }

        public static void Clear()
        {
            backend = null;
        }
    }
}
