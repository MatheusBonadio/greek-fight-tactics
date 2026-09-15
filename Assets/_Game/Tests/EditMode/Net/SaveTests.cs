using System.IO;
using System.Linq;
using Game.Core;
using Game.Grid;
using Game.Net;
using Game.Tests.EditMode.Grid;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests.EditMode.Net
{
    public sealed class SaveTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), $"gft-save-{System.Guid.NewGuid():N}");
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
        public void Json_UsesTheFirestoreDocumentFieldNames()
        {
            var document = JObject.Parse(SaveJson.Serialize(BuildSave()));

            CollectionAssert.IsSupersetOf(
                ((JObject)document).Properties().Select(property => property.Name).ToList(),
                new[] { "actFlag", "fogBitmask", "damarisBranch", "reputation", "inventory", "position", "timestamp", "duels" });
            Assert.AreEqual("accepted", (string)document["damarisBranch"]);
            Assert.AreEqual("maieutics", (string)document["duels"]["batilo"]["victory"]);
        }

        [Test]
        public void Json_RoundTripsEveryField_IncludingTheFogBitmask()
        {
            var grid = GridFactory.Open(64, 64);
            var fog = new FogOfWar(grid);
            fog.Reveal(new Coord(10, 12), 5);
            var original = BuildSave();
            original.FogBitmask = fog.ToBitmask();

            Assert.IsTrue(SaveJson.TryDeserialize(SaveJson.Serialize(original), out var restored, out var error), error);

            Assert.AreEqual(original.ActFlag, restored.ActFlag);
            CollectionAssert.AreEqual(original.FogBitmask, restored.FogBitmask);
            Assert.AreEqual(original.DamarisBranch, restored.DamarisBranch);
            Assert.AreEqual(original.Reputation, restored.Reputation);
            CollectionAssert.AreEquivalent(original.Inventory, restored.Inventory);
            Assert.AreEqual(original.Position, restored.Position);
            Assert.AreEqual(original.Timestamp, restored.Timestamp);
            Assert.AreEqual(DuelVictory.Maieutics, restored.Duels["batilo"].Victory);
            Assert.AreEqual("yes", restored.Flags["met_menexeno"]);

            var reloadedGrid = GridFactory.Open(64, 64);
            Assert.IsTrue(new FogOfWar(reloadedGrid).TryLoadBitmask(restored.FogBitmask));
            Assert.IsTrue(reloadedGrid[new Coord(10, 12)].Revealed);
        }

        [TestCase("not json")]
        [TestCase("{\"actFlag\": 7}")]
        [TestCase("{\"actFlag\": 1, \"fogBitmask\": \"***\"}")]
        [TestCase("{\"actFlag\": 1, \"damarisBranch\": \"maybe\"}")]
        [TestCase("{\"actFlag\": 1, \"duels\": {\"x\": {\"won\": true, \"victory\": \"luck\"}}}")]
        public void Json_RejectsCorruptSaves(string json)
        {
            Assert.IsFalse(SaveJson.TryDeserialize(json, out var save, out var error));
            Assert.IsNull(save);
            Assert.IsNotNull(error);
        }

        [Test]
        public void LocalStore_SavesAtomicallyAndLoadsBack()
        {
            var store = new LocalSaveStore(directory);
            Assert.IsFalse(store.TryLoad(out _, out _));

            store.Save(BuildSave());

            Assert.IsTrue(store.Exists);
            Assert.IsFalse(File.Exists(store.FilePath + ".tmp"));
            Assert.IsTrue(store.TryLoad(out var loaded, out var error), error);
            Assert.AreEqual(12, loaded.Reputation);
            Assert.IsTrue(store.Delete());
            Assert.IsFalse(store.Exists);
        }

        [Test]
        public void ConflictResolver_KeepsTheNewestTimestamp()
        {
            var local = new SaveData { Timestamp = 100, Reputation = 1 };
            var remote = new SaveData { Timestamp = 200, Reputation = 2 };

            Assert.AreSame(remote, SaveConflictResolver.PickNewest(local, remote));
            Assert.AreSame(local, SaveConflictResolver.PickNewest(local, new SaveData { Timestamp = 100 }));
            Assert.AreSame(local, SaveConflictResolver.PickNewest(local, null));
            Assert.AreSame(remote, SaveConflictResolver.PickNewest(null, remote));
        }

        [TestCase("act", "2", true)]
        [TestCase("act", "3", false)]
        [TestCase("damaris_branch", "refused", true)]
        [TestCase("damaris_branch", "maybe", false)]
        [TestCase("reputation", "-5", true)]
        [TestCase("reputation", "lots", false)]
        [TestCase("position", "4, 7", true)]
        [TestCase("position", "4", false)]
        [TestCase("met_pythia", "true", true)]
        [TestCase("Bad Flag", "x", false)]
        public void Flags_ValidateKnownAndCustomNames(string name, string value, bool accepted)
        {
            var save = new SaveData();

            Assert.AreEqual(accepted, SaveFlags.TrySet(save, name, value, out var error), error);
        }

        private static SaveData BuildSave()
        {
            var save = new SaveData
            {
                ActFlag = Act.Two,
                FogBitmask = new byte[] { 1, 2, 255 },
                DamarisBranch = DamarisChoice.Accepted,
                Reputation = 12,
                Position = new Coord(7, 3),
                Timestamp = 1789480000000
            };
            save.Inventory["heraclitus"] = 2;
            save.Duels["batilo"] = new DuelRecord(true, DuelVictory.Maieutics);
            save.Flags["met_menexeno"] = "yes";
            return save;
        }
    }
}
