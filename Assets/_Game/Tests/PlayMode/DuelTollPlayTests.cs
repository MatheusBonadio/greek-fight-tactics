using System.Collections;
using System.Linq;
using Game.Battle;
using Game.Core;
using Game.Grid;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class DuelTollPlayTests
    {
        private const string Rival = "simo";
        private const float TimeoutSeconds = 10f;
        private const float BattleTimeoutSeconds = 60f;

        private static readonly Coord HomeArrival = new(16, 2);

        private IsolatedDataDirectory data;
        private SandboxBattleDirector director;
        private GridSandboxController sandbox;
        private ExplorationDialogueDirector story;
        private GameServices services;

        [UnitySetUp]
        public IEnumerator OpenTheAgora()
        {
            data = IsolatedDataDirectory.Enter();
            SceneArrival.Clear();
            yield return SceneManager.LoadSceneAsync(LocationScenes.AgoraScene, LoadSceneMode.Single);
            yield return null;

            director = Object.FindAnyObjectByType<SandboxBattleDirector>();
            sandbox = Object.FindAnyObjectByType<GridSandboxController>();
            story = Object.FindAnyObjectByType<ExplorationDialogueDirector>();
            services = Object.FindAnyObjectByType<GameServices>();
            director.ThinkSeconds = 0f;
            if (director.Crowd)
            {
                director.Crowd.ArrivalCapSeconds = 0f;
            }

            Object.FindAnyObjectByType<StoryCueDirector>().enabled = false;
            yield return DuelChallenge.WaitFor(() => sandbox.ProgressLoaded && story.IsStoryLoaded && services.IsBooted, TimeoutSeconds);
            Assert.IsTrue(story.IsStoryLoaded);
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
        public IEnumerator ADuelLeftBehind_FollowsKleiaIntoTheNextOneAndSleepsOffAtHome()
        {
            var full = BattleSession.PlayerMaxCoherence;
            Assert.IsTrue(story.Remember(StoryStamina.Toll(DuelStoryFlags.Lost, 0, full)), "the duel she lost wore her down");
            Assert.AreEqual(StoryStamina.Bruised, StoryStamina.Carried(story.Story, full));

            var rival = director.Opponents.Single(entry => entry.Opponent.Id == Rival);
            yield return DuelChallenge.Engage(sandbox, director, rival, TimeoutSeconds);

            var player = director.Session.State.Player;
            Assert.AreEqual(full, player.MaxCoherence, "the bar still measures a whole Kleia");
            Assert.AreEqual(StoryStamina.Bruised, player.Coherence, "she opens the duel as bruised as she left the last one");

            var reputation = story.Story.Reputation;
            yield return AdvanceUntil(() => director.Phase == BattleFlowPhase.PlayerChoosing);
            director.SubmitChoice(new BattleMenuChoice(ActionId.Withdraw));
            yield return AdvanceUntil(() => director.Phase == BattleFlowPhase.Exploring);

            Assert.AreEqual(BattleStatus.PlayerWithdrew, director.LastResult);
            Assert.AreEqual(StoryStamina.Bruised, StoryStamina.Carried(story.Story, full), "fleeing on her last breath cannot take her below the floor");
            Assert.AreEqual(ReputationRules.After(new SaveData { Reputation = reputation }, DuelStoryFlags.Withdrew), story.Story.Reputation, "running away costs her name");

            SceneArrival.Set(new PendingArrival(LocationScenes.Kerameikos, HomeArrival, Facing.SouthWest, LocationScenes.Agora));
            yield return SceneManager.LoadSceneAsync(LocationScenes.KerameikosScene, LoadSceneMode.Single);
            var home = Object.FindAnyObjectByType<GridSandboxController>();
            var homeServices = Object.FindAnyObjectByType<GameServices>();
            yield return DuelChallenge.WaitFor(() => homeServices.IsBooted && home.ProgressLoaded, TimeoutSeconds);

            Assert.IsTrue(homeServices.TryLoadActiveSave(out var saved));
            Assert.IsTrue(StoryStamina.IsRested(saved), "a night at the workshop puts her back together");
            Assert.AreEqual(BattleSession.PlayerMaxCoherence, StoryStamina.Carried(saved, full));
            Assert.AreEqual(StoryDays.FirstDay + 1, StoryDays.DayOf(saved), "coming home passes a day");
        }

        private IEnumerator AdvanceUntil(System.Func<bool> done)
        {
            var deadline = Time.realtimeSinceStartup + BattleTimeoutSeconds;
            while (!done() && Time.realtimeSinceStartup < deadline)
            {
                if (director.Phase == BattleFlowPhase.Dialogue)
                {
                    director.AdvanceDialogue();
                }

                yield return null;
            }

            Assert.IsTrue(done(), $"the duel got stuck in {director.Phase}");
        }
    }
}
