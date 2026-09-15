using UnityEngine;

namespace Game.Content
{
    public sealed class NetworkConfigSO : ScriptableObject
    {
        public const string DefaultRegion = "southamerica-east1";
        public const string DefaultEmulatorHost = "127.0.0.1";

        [SerializeField] private string projectId;
        [SerializeField] private string webApiKey;
        [SerializeField] private string region = DefaultRegion;
        [SerializeField] private bool useEmulator;
        [SerializeField] private string emulatorHost = DefaultEmulatorHost;

        public string ProjectId => projectId;

        public string WebApiKey => webApiKey;

        public string Region => string.IsNullOrWhiteSpace(region) ? DefaultRegion : region;

        public bool UseEmulator => useEmulator;

        public string EmulatorHost => string.IsNullOrWhiteSpace(emulatorHost) ? DefaultEmulatorHost : emulatorHost;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(projectId) && (useEmulator || !string.IsNullOrWhiteSpace(webApiKey));

        public void Configure(string newProjectId, string newWebApiKey, bool newUseEmulator, string newRegion = DefaultRegion, string newEmulatorHost = DefaultEmulatorHost)
        {
            projectId = newProjectId;
            webApiKey = newWebApiKey;
            useEmulator = newUseEmulator;
            region = newRegion;
            emulatorHost = newEmulatorHost;
        }
    }
}
