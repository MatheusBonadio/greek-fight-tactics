using System.Linq;
using Game.EditorTools;
using Game.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tests.EditMode.UI
{
    public sealed class KerameikosSceneTests
    {
        private const string PlayerCharacter = "kleia";

        private Scene scene;

        [OneTimeSetUp]
        public void OpenScene()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(SandboxTools.KerameikosScenePath), $"run SandboxTools.BuildSandbox to create {SandboxTools.KerameikosScenePath}");
            scene = EditorSceneManager.OpenScene(SandboxTools.KerameikosScenePath, OpenSceneMode.Additive);
        }

        [OneTimeTearDown]
        public void CloseScene()
        {
            EditorSceneManager.CloseScene(scene, true);
        }

        [Test]
        public void Workshop_IsItsOwnLocationWithKleiaFirstAndNoBattle()
        {
            var controllers = Components<GridSandboxController>();

            Assert.AreEqual(1, controllers.Length);
            Assert.AreEqual(SandboxTools.KerameikosLocationId, controllers[0].LocationId);
            Assert.AreEqual(SandboxTools.Size * SandboxTools.Size, controllers[0].Terrain.Count);
            StringAssert.StartsWith(PlayerCharacter, controllers[0].Actors[0].name);
            CollectionAssert.IsEmpty(Components<SandboxBattleDirector>());
        }

        [Test]
        public void BuildSettings_IncludeTheWorkshop()
        {
            CollectionAssert.Contains(EditorBuildSettings.scenes.Select(entry => entry.path).ToArray(), SandboxTools.KerameikosScenePath);
        }

        private T[] Components<T>() where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }
}
