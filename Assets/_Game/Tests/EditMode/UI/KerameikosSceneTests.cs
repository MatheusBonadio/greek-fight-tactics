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
        private const string AristeuCharacter = "aristeu";

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
            var map = MapLayout.Load(SandboxTools.KerameikosMapPath);
            Assert.AreEqual(map.Width, controllers[0].Width);
            Assert.AreEqual(map.Height, controllers[0].Height);
            Assert.AreEqual(map.Width * map.Height, controllers[0].Terrain.Count);
            Assert.AreEqual(map.FogRevealDepth, controllers[0].FogRevealDepth);
            StringAssert.StartsWith(PlayerCharacter, controllers[0].Actors[0].name);
            CollectionAssert.IsEmpty(Components<SandboxBattleDirector>());
        }

        [Test]
        public void Workshop_WiresTheTutorialCuesAristeuAndMenexenoWaitingForHisFlags()
        {
            var map = MapLayout.Load(SandboxTools.KerameikosMapPath);
            var controller = Components<GridSandboxController>().Single();
            var cues = Components<StoryCueDirector>().Single();

            CollectionAssert.AreEquivalent(
                map.Cues.Select(cue => $"{cue.On}:{cue.Dialogue}:{cue.Flag}:{string.Join(",", cue.StoryConditions)}"),
                cues.Cues.Select(link => $"{link.On}:{link.Dialogue}:{link.Flag}:{string.Join(",", link.Conditions)}"));

            var conditional = map.Actors.Where(actor => actor.IsConditional).ToList();
            CollectionAssert.AreEquivalent(
                conditional.Select(actor => $"{actor.X},{actor.Y}:{string.Join(",", actor.StoryConditions)}"),
                controller.Presences.Select(link => $"{link.Actor.StartCell.x},{link.Actor.StartCell.y}:{string.Join(",", link.Conditions)}"));

            var aristeu = controller.Actors.Single(actor => actor.name.StartsWith(AristeuCharacter));
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<Texture2D>(ArtTools.SheetPathFor(AristeuCharacter)), aristeu.SpriteRenderer.sprite.texture);
        }

        [Test]
        public void Workshop_PlaysEveryVisitFromATimelineBuiltFromTheMap()
        {
            var map = MapLayout.Load(SandboxTools.KerameikosMapPath);
            var director = Components<CutsceneDirector>().Single();
            var dialogues = Components<ExplorationDialogueDirector>().Single();

            Assert.AreSame(director, dialogues.Cutscenes, "cutscene triggers reach the director");
            Assert.IsNotNull(director.Director);
            Assert.AreSame(director.gameObject, director.Director.gameObject, "the markers notify the director that owns the PlayableDirector");
            Assert.IsFalse(director.Director.playOnAwake);
            CollectionAssert.AreEquivalent(map.Cutscenes.Select(cutscene => cutscene.Id), director.Cutscenes.Keys);
            foreach (var cutscene in map.Cutscenes)
            {
                Assert.IsTrue(director.Cutscenes.TryGetValue(cutscene.Id, out var timeline), cutscene.Id);
                Assert.AreEqual(CutsceneTools.TimelinePathFor(map.Id, cutscene.Id), AssetDatabase.GetAssetPath(timeline));

                var markers = timeline.markerTrack.GetMarkers().OfType<CutsceneMarker>().OrderBy(marker => marker.time).ToList();
                Assert.AreEqual(cutscene.Steps.Count, markers.Count, cutscene.Id);
                Assert.AreEqual(markers.Count, timeline.markerTrack.GetMarkerCount(), "the timeline holds only cutscene markers");
                for (var index = 0; index < markers.Count; index++)
                {
                    var step = cutscene.Steps[index];
                    var where = $"{cutscene.Id} step {index}";
                    Assert.AreEqual(step.At, markers[index].time, 1e-6, where);
                    Assert.AreEqual(step.Action, markers[index].Action, where);
                    Assert.AreEqual(step.Actor ?? string.Empty, markers[index].Actor, where);
                    Assert.AreEqual(step.Toward ?? string.Empty, markers[index].Toward, where);
                    Assert.AreEqual(step.Dialogue ?? string.Empty, markers[index].Dialogue, where);
                    Assert.AreEqual(step.HasCell, markers[index].HasCell, where);
                }

                Assert.Greater(timeline.duration, cutscene.LastStepAt, "the timeline runs past its last marker");
            }

            CollectionAssert.AreEquivalent(map.Cutscenes.SelectMany(cutscene => cutscene.ActorIds).Distinct(), director.Cast.Keys);
            foreach (var member in director.Cast)
            {
                StringAssert.StartsWith(member.Key, member.Value.name);
            }

            CollectionAssert.AreEquivalent(
                map.Triggers.Where(trigger => trigger.IsCutscene).Select(trigger => $"{trigger.X},{trigger.Y}:{trigger.Cutscene}"),
                dialogues.Triggers.Where(link => link.IsCutscene).Select(link => $"{link.Cell.X},{link.Cell.Y}:{link.Cutscene}"));
        }

        [Test]
        public void Workshop_IsHomeAndHidesItsConditionalBlocksOnlyThroughStoryFlags()
        {
            var map = MapLayout.Load(SandboxTools.KerameikosMapPath);
            var controller = Components<GridSandboxController>().Single();

            Assert.IsTrue(controller.IsHome, "coming back to the workshop ends the day");
            CollectionAssert.AreEquivalent(
                map.ConditionalBlocks.Select(block => $"{block.X},{block.Y}:{string.Join(",", block.StoryConditions)}"),
                controller.BlockPresences.Select(link => $"{link.Cell.X},{link.Cell.Y}:{string.Join(",", link.Conditions)}"));
            foreach (var link in controller.BlockPresences)
            {
                var index = link.Cell.Y * map.Width + link.Cell.X;
                Assert.IsNotNull(controller.BlockTiles[index], $"the block at {link.Cell} is baked into the scene");
                Assert.AreEqual(Game.Grid.TileData.ImpassableCost, controller.Costs[index], $"the block at {link.Cell} starts impassable");
                Assert.AreEqual(SandboxTools.FloorCost(map.FloorAt(link.Cell.X, link.Cell.Y)), link.OpenCost, $"without the block, {link.Cell} costs what its floor costs");
            }
        }

        [Test]
        public void Aristeu_SpeaksWithTheDedicatedPortraitOfHisHands()
        {
            var dialogues = Components<ExplorationDialogueDirector>().Single();

            Assert.IsTrue(dialogues.Portraits.TryGetValue(AristeuCharacter, out var portrait));
            Assert.AreEqual(ArtTools.PortraitPathFor(AristeuCharacter), AssetDatabase.GetAssetPath(portrait));
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
