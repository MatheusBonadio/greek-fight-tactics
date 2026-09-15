using System.Collections;
using Game.DevCommands;
using Game.UI;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public sealed class GridCommandsPlayTests
    {
        [UnitySetUp]
        public IEnumerator LoadSandbox()
        {
            yield return SceneManager.LoadSceneAsync("GridSandbox", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GridSpawnAndClear_DriveTheRunningSandbox()
        {
            var controller = Object.FindAnyObjectByType<GridSandboxController>();
            var before = controller.Actors.Count;

            var first = JObject.Parse(GftCommands.GridSpawn(4, 4));
            var second = JObject.Parse(GftCommands.GridSpawn(4, 5));
            var blocked = JObject.Parse(GftCommands.GridSpawn(4, 4));
            yield return null;

            Assert.IsTrue((bool)first["success"]);
            Assert.IsTrue((bool)second["success"]);
            Assert.IsFalse((bool)blocked["success"]);
            Assert.AreEqual(before + 2, (int)second["actor_count"]);

            var cleared = JObject.Parse(GftCommands.GridClear());
            yield return null;

            Assert.AreEqual(2, (int)cleared["affected"]);
            Assert.AreEqual(before, controller.Actors.Count);
        }
    }
}
