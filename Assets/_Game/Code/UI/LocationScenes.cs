namespace Game.UI
{
    public static class LocationScenes
    {
        public const string Kerameikos = "kerameikos";
        public const string Agora = "agora";
        public const string Archive = "archive";
        public const string Academy = "academy";
        public const string KerameikosScene = "Kerameikos";
        public const string AgoraScene = "GridSandbox";
        public const string ArchiveScene = "Archive";
        public const string AcademyScene = "Academy";
        public const string StartScene = KerameikosScene;

        public static string SceneFor(string locationId) => locationId switch
        {
            Kerameikos => KerameikosScene,
            Agora => AgoraScene,
            Archive => ArchiveScene,
            Academy => AcademyScene,
            GridSandboxController.DefaultLocationId => AgoraScene,
            _ => StartScene
        };
    }
}
