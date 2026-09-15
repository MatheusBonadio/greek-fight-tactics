using System.IO;
using System.Linq;
using Game.DevCommands;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.DevCommands
{
    public sealed class GftCommandsTests
    {
        private const string ValidArgument = @"{
            ""speech"": ""Todo homem justo é temido pelos injustos. Sócrates é temido. Logo, Sócrates é justo."",
            ""fallacy_id"": ""affirming_the_consequent"",
            ""premises"": [""Todo justo é temido pelos injustos"", ""Sócrates é temido""],
            ""weak_premise"": 1,
            ""tone"": ""arrogant""
        }";

        private string temporaryFile;

        [SetUp]
        public void SetUp()
        {
            temporaryFile = Path.Combine(Path.GetTempPath(), $"gft-argument-{System.Guid.NewGuid():N}.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(temporaryFile))
            {
                File.Delete(temporaryFile);
            }
        }

        [Test]
        public void ValidateArgument_ReportsAValidFileAsJson()
        {
            File.WriteAllText(temporaryFile, ValidArgument);

            var response = JObject.Parse(GftCommands.ValidateArgument(temporaryFile));

            Assert.IsTrue((bool)response["valid"]);
            Assert.AreEqual("None", (string)response["items"][0]["violation"]);
        }

        [Test]
        public void ValidateArgument_ReportsEveryBrokenItemInAnArray()
        {
            var broken = ValidArgument.Replace("affirming_the_consequent", "invented_fallacy");
            File.WriteAllText(temporaryFile, $"[{ValidArgument}, {broken}]");

            var response = JObject.Parse(GftCommands.ValidateArgument(temporaryFile));

            Assert.IsFalse((bool)response["valid"]);
            Assert.AreEqual("None", (string)response["items"][0]["violation"]);
            Assert.AreEqual("UnknownFallacy", (string)response["items"][1]["violation"]);
        }

        [Test]
        public void ValidateArgument_WithMissingFile_ReturnsAJsonError()
        {
            var response = JObject.Parse(GftCommands.ValidateArgument("does/not/exist.json"));

            Assert.IsFalse((bool)response["success"]);
            StringAssert.Contains("file not found", (string)response["error"]);
        }

        [Test]
        public void BattleStart_ReturnsTheOpeningStateWithTheOpponentsArgument()
        {
            var response = JObject.Parse(GftCommands.BattleStart("philosopher", "zeno_tortoise", 2));

            Assert.IsTrue((bool)response["success"]);
            Assert.AreEqual("Ongoing", (string)response["status"]);
            Assert.AreEqual("Two", (string)response["act"]);
            Assert.AreEqual(1, (int)response["turn"]);
            Assert.AreEqual(JTokenType.Null, response["current_argument"]["fallacy_id"].Type);
            Assert.AreEqual(2, response["debaters"].Count());
            Assert.AreEqual(3, response["upcoming_actor_ids"].Count());
            Assert.AreEqual("Argue", (string)response["log"][0]["action"]);
        }

        [TestCase("wizard", "ad_hominem", 1)]
        [TestCase("sophist", "not_in_catalog", 1)]
        [TestCase("sophist", "ad_hominem", 3)]
        public void BattleStart_WithBadInput_ReturnsAJsonError(string opponent, string fallacy, int act)
        {
            var response = JObject.Parse(GftCommands.BattleStart(opponent, fallacy, act));

            Assert.IsFalse((bool)response["success"]);
        }

        [Test]
        public void BattleSimulate_ReturnsWinRatesAndTheFirstResolverLog()
        {
            var response = JObject.Parse(GftCommands.BattleSimulate(20, 1, "sophist"));

            var total = (int)response["wins"] + (int)response["losses"] + (int)response["withdrawals"] + (int)response["turn_limit"];
            Assert.AreEqual(20, (int)response["battles"]);
            Assert.AreEqual(20, total);
            Assert.Greater((double)response["average_turns"], 0);
            Assert.IsNotEmpty(response["first_battle_log"]);
        }

        [TestCase(0)]
        [TestCase(1001)]
        public void BattleSimulate_RejectsBattleCountsOutsideTheLimit(int battles)
        {
            Assert.IsFalse((bool)JObject.Parse(GftCommands.BattleSimulate(battles))["success"]);
        }

        [Test]
        public void GridCommands_OutsidePlayMode_ReturnJsonErrors()
        {
            Assert.IsFalse(Application.isPlaying);
            StringAssert.Contains("not running", (string)JObject.Parse(GftCommands.GridSpawn(1, 1))["error"]);
            StringAssert.Contains("not running", (string)JObject.Parse(GftCommands.GridClear())["error"]);
        }

        [Test]
        public void ContentPaths_MatchTheEditorGenerator()
        {
            var catalogPath = typeof(GftCommands).Assembly.GetType("Game.DevCommands.DevContent")
                .GetField("CatalogPath").GetRawConstantValue();

            Assert.AreEqual(Game.EditorTools.DataTools.CatalogPath, catalogPath);
        }
    }
}
