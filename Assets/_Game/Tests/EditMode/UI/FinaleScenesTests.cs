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
    public sealed class FinaleScenesTests
    {
        [TestCase(SandboxTools.ArchiveScenePath, SandboxTools.ArchiveLocationId)]
        [TestCase(SandboxTools.AcademyScenePath, SandboxTools.AcademyLocationId)]
        public void Scene_IsItsOwnLocationInTheBuildWithoutABattle(string scenePath, string locationId)
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath), $"run SandboxTools.BuildSandbox to create {scenePath}");
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                Assert.AreEqual(locationId, Find<GridSandboxController>(scene).LocationId);
                Assert.IsNotNull(Find<ExplorationDialogueDirector>(scene), "every map talks through the dialogue director");
                Assert.IsNotNull(Find<StoryCueDirector>(scene), "the map opens with its own cue");
                Assert.IsNull(Find<SandboxBattleDirector>(scene), "nobody picks a fight here");
                CollectionAssert.Contains(EditorBuildSettings.scenes.Select(entry => entry.path), scenePath);
                Assert.AreEqual(System.IO.Path.GetFileNameWithoutExtension(scenePath), LocationScenes.SceneFor(locationId));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void Archive_PlaysTheJurorListFromATimelineWithTheAcademicInTheCast()
        {
            var scene = EditorSceneManager.OpenScene(SandboxTools.ArchiveScenePath, OpenSceneMode.Additive);
            try
            {
                var cutscenes = Find<CutsceneDirector>(scene);
                Assert.IsNotNull(cutscenes);
                Assert.IsTrue(cutscenes.Contains("archive_list_scene"));
                CollectionAssert.AreEquivalent(new[] { "kleia", "academic" }, cutscenes.Cast.Keys);
                var controller = Find<GridSandboxController>(scene);
                Assert.IsTrue(controller.Presences.Any(presence => presence.Actor == cutscenes.Cast["academic"]), "the academic only waits until the seal is handed over");
                StringAssert.StartsWith("citizen_", cutscenes.Cast["academic"].SpriteRenderer.sprite.name, "until he gets his own sprite, the academic wears the citizen sheet");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static T Find<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault();
    }
}
