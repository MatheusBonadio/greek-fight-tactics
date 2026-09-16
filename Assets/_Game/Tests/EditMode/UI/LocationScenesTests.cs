using Game.UI;
using NUnit.Framework;

namespace Game.Tests.EditMode.UI
{
    public sealed class LocationScenesTests
    {
        [TestCase(LocationScenes.Kerameikos, LocationScenes.KerameikosScene)]
        [TestCase(LocationScenes.Agora, LocationScenes.AgoraScene)]
        [TestCase(GridSandboxController.DefaultLocationId, LocationScenes.AgoraScene)]
        [TestCase("", LocationScenes.KerameikosScene)]
        [TestCase(null, LocationScenes.KerameikosScene)]
        [TestCase("delphi", LocationScenes.KerameikosScene)]
        public void SceneFor_LoadsTheSavedMapAndFallsBackToTheWorkshop(string locationId, string expectedScene)
        {
            Assert.AreEqual(expectedScene, LocationScenes.SceneFor(locationId));
        }

        [Test]
        public void NewGames_StartInTheKerameikosWorkshop()
        {
            Assert.AreEqual(LocationScenes.KerameikosScene, LocationScenes.StartScene);
        }
    }
}
