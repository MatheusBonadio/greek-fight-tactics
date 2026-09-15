using Game.Core;
using Game.EditorTools;
using Game.UI;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Tests.EditMode.UI
{
    public sealed class BattleCameraDirectorTests
    {
        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
        }

        [Test]
        public void EnteringAndLeavingBattle_ZoomsTheExplorationCamera()
        {
            host = new GameObject("camera");
            var virtualCamera = host.AddComponent<CinemachineCamera>();
            var explorationCamera = host.AddComponent<ExplorationCamera>();
            explorationCamera.Configure(virtualCamera, 7f, 5f);
            var director = host.AddComponent<BattleCameraDirector>();
            director.Configure(explorationCamera);
            var machine = new GameStateMachine();
            director.Bind(machine);

            machine.TryTransition(GameMode.BattleIntro);
            Assert.AreEqual(5f, virtualCamera.Lens.OrthographicSize);

            machine.TryTransition(GameMode.BattleTurn);
            machine.TryTransition(GameMode.BattleResolve);
            machine.TryTransition(GameMode.BattleOutro);
            Assert.AreEqual(5f, virtualCamera.Lens.OrthographicSize);

            machine.TryTransition(GameMode.Exploration);
            Assert.AreEqual(7f, virtualCamera.Lens.OrthographicSize);
        }

        [Test]
        public void ArgumentWorkbench_DescribesReportsAndPreviewsDialogue()
        {
            var validator = new ArgumentValidator(new[] { "ad_hominem" });
            var report = Game.Net.ArgumentValidationReport.Validate("not json", validator);

            StringAssert.StartsWith("Invalid JSON", ArgumentWorkbenchWindow.DescribeReport(report));

            var argument = new Argument(new string('a', 30) + " " + new string('b', 30), "ad_hominem", new[] { "p" }, 0, ArgumentTones.Mocking);
            StringAssert.Contains("\n", ArgumentWorkbenchWindow.PreviewDialogue(argument));

            var window = ScriptableObject.CreateInstance<ArgumentWorkbenchWindow>();
            Assert.IsNotNull(window);
            Object.DestroyImmediate(window);
        }
    }
}
