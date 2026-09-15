using System.IO;
using Game.DevCommands;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests.EditMode.DevCommands
{
    public sealed class SaveCommandsTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), $"gft-save-commands-{System.Guid.NewGuid():N}");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void SetFlag_CreatesTheSaveAndSaveDumpReadsItBack()
        {
            var set = JObject.Parse(SaveCommands.SetFlag("act", "2", directory));
            JObject.Parse(SaveCommands.SetFlag("met_pythia", "true", directory));
            var dump = JObject.Parse(SaveCommands.SaveDump(directory));

            Assert.IsTrue((bool)set["success"]);
            Assert.IsTrue((bool)dump["success"]);
            Assert.AreEqual(2, (int)dump["save"]["actFlag"]);
            Assert.AreEqual("true", (string)dump["save"]["flags"]["met_pythia"]);
            Assert.Greater((long)dump["save"]["timestamp"], 0);
        }

        [Test]
        public void SetFlag_WithInvalidValue_ReturnsAJsonErrorAndKeepsTheSave()
        {
            JObject.Parse(SaveCommands.SetFlag("reputation", "5", directory));

            var failed = JObject.Parse(SaveCommands.SetFlag("reputation", "many", directory));
            var dump = JObject.Parse(SaveCommands.SaveDump(directory));

            Assert.IsFalse((bool)failed["success"]);
            Assert.AreEqual(5, (int)dump["save"]["reputation"]);
        }

        [Test]
        public void SaveLoad_InstallsAValidFile_AndRejectsACorruptOne()
        {
            Directory.CreateDirectory(directory);
            var validFile = Path.Combine(directory, "incoming.json");
            File.WriteAllText(validFile, "{\"actFlag\": 2, \"reputation\": 40, \"damarisBranch\": \"refused\", \"timestamp\": 5}");
            var corruptFile = Path.Combine(directory, "corrupt.json");
            File.WriteAllText(corruptFile, "{\"actFlag\": 9}");

            var loaded = JObject.Parse(SaveCommands.SaveLoad(validFile, directory, "slot1"));
            var rejected = JObject.Parse(SaveCommands.SaveLoad(corruptFile, directory, "slot1"));
            var dump = JObject.Parse(SaveCommands.SaveDump(directory, "slot1"));

            Assert.IsTrue((bool)loaded["success"]);
            Assert.IsFalse((bool)rejected["success"]);
            Assert.AreEqual("refused", (string)dump["save"]["damarisBranch"]);
            Assert.AreEqual(40, (int)dump["save"]["reputation"]);
        }

        [Test]
        public void SaveDump_WithoutASave_ReturnsAJsonError()
        {
            Assert.IsFalse((bool)JObject.Parse(SaveCommands.SaveDump(directory))["success"]);
        }
    }
}
