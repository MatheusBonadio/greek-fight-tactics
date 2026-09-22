using System.Linq;
using Game.EditorTools;
using Game.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tests.EditMode.UI
{
    public sealed class ExplorationDialogueSceneTests
    {
        [Test]
        public void Prefab_HoldsAClosedDialogueBoxOnAnOverlayCanvasBelowThePauseMenu()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattleHudTools.ExplorationDialoguePrefabPath);
            Assert.IsNotNull(prefab, $"run BattleHudTools.BuildHud to create {BattleHudTools.ExplorationDialoguePrefabPath}");

            var canvas = prefab.GetComponent<Canvas>();
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.Less(canvas.sortingOrder, AssetDatabase.LoadAssetAtPath<GameObject>(SystemMenuTools.PauseMenuPrefabPath).GetComponent<Canvas>().sortingOrder);

            var box = prefab.GetComponentInChildren<DialogueBoxView>(true);
            Assert.IsNotNull(box);
            Assert.IsFalse(box.IsOpen);
            Assert.IsNotNull(box.Body);
            Assert.IsNotNull(box.SpeakerLabel);
            Assert.IsNotNull(box.Portrait);
        }

        [TestCase(SandboxTools.MapPath, SandboxTools.ScenePath)]
        [TestCase(SandboxTools.KerameikosMapPath, SandboxTools.KerameikosScenePath)]
        public void Scene_WiresTheTalkingNpcsAndCellTriggersOfItsMap(string mapPath, string scenePath)
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath), $"run SandboxTools.BuildSandbox to create {scenePath}");
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var map = MapLayout.Load(mapPath);
                var director = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ExplorationDialogueDirector>(true)).Single();

                Assert.IsNotNull(director.DialogueBox);
                Assert.IsFalse(director.DialogueBox.IsOpen);
                Assert.IsNotNull(director.Library);
                CollectionAssert.AreEquivalent(
                    map.Actors.Where(actor => !string.IsNullOrEmpty(actor.Dialogue)).Select(actor => $"{actor.X},{actor.Y}:{actor.Dialogue}"),
                    director.Talkers.Select(link => $"{link.Actor.StartCell.x},{link.Actor.StartCell.y}:{link.Dialogue}"));
                CollectionAssert.AreEquivalent(
                    map.Triggers.Select(trigger => $"{trigger.X},{trigger.Y}:{trigger.Dialogue}:{trigger.Cutscene}:{trigger.Flag}"),
                    director.Triggers.Select(link => $"{link.Cell.X},{link.Cell.Y}:{link.Dialogue}:{link.Cutscene}:{link.Flag}"));

                foreach (var speaker in director.Library.Speakers.Where(pair => pair.Value.HasPortrait))
                {
                    Assert.IsTrue(director.Portraits.TryGetValue(speaker.Key, out var portrait) && portrait != null, $"{speaker.Key} has no portrait in {scenePath}");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
