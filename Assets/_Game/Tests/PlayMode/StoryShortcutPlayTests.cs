using System.Collections;
using System.Linq;
using Game.Core;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class StoryShortcutPlayTests
    {
        private const float TimeoutSeconds = 10f;
        private const string TrialOpponent = "thrasybios_street_trial";

        private IsolatedDataDirectory data;

        [UnitySetUp]
        public IEnumerator IsolateData()
        {
            data = IsolatedDataDirectory.Enter();
            SceneArrival.Clear();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator RestoreData()
        {
            SceneArrival.Clear();
            data?.Dispose();
            data = null;
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheShortcut_CarriesANewGameUpToTheStreetTrial()
        {
            yield return SceneManager.LoadSceneAsync(LocationScenes.AgoraScene, LoadSceneMode.Single);
            yield return null;

            var sandbox = Object.FindAnyObjectByType<GridSandboxController>();
            var story = Object.FindAnyObjectByType<ExplorationDialogueDirector>();
            var services = Object.FindAnyObjectByType<GameServices>();
            var battle = Object.FindAnyObjectByType<SandboxBattleDirector>();
            Object.FindAnyObjectByType<StoryCueDirector>().enabled = false;
            yield return DuelChallenge.WaitFor(() => sandbox.ProgressLoaded && story.IsStoryLoaded && services.IsBooted, TimeoutSeconds);

            Assert.AreSame(story, sandbox.StoryShortcut, "the debug panel reaches the story through the director");
            Assert.AreEqual(ExplorationDialogueDirector.NewGameLabel, story.ReachedLabel);
            Assert.AreEqual(StoryCheckpoints.All[0].Label, story.NextLabel);

            var trial = battle.Opponents.Single(entry => entry.Opponent.Id == TrialOpponent);
            Assert.IsFalse(trial.IsPlaced, "the street trial waits for the story");

            foreach (var checkpoint in StoryCheckpoints.All.TakeWhile(entry => entry.Id != "searching"))
            {
                Assert.IsTrue(story.SkipToNextCheckpoint(), $"the shortcut refused {checkpoint.Id}");
                Assert.AreEqual(checkpoint.Label, story.ReachedLabel);
                yield return null;
            }

            Assert.IsTrue(ActionUnlocks.IsUnlocked(ActionId.Maieutics, story.Story), "the skipped lessons unlock maieutics");
            Assert.AreEqual(DamarisChoice.Refused, story.Story.DamarisBranch);
            yield return DuelChallenge.WaitFor(() => trial.IsPlaced, TimeoutSeconds);
            Assert.IsTrue(trial.IsPlaced, "skipping to the herald puts Thrasybios on the steps");

            Assert.IsTrue(services.TryLoadActiveSave(out var saved), "the shortcut saves what it skipped");
            Assert.IsTrue(StoryCondition.Has("street_trial_called").IsMet(saved));
            Assert.AreEqual(StoryCheckpoints.All.First(entry => entry.Id == "trial").Label, story.ReachedLabel);
        }
    }
}
