using System.IO;
using System.Linq;
using Game.Core;
using Game.Net;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Game.Tests.EditMode.Net.NetSamples;

namespace Game.Tests.EditMode.Net
{
    public sealed class FirestoreDocumentsTests
    {
        private static string GoldenCommitPath => Path.Combine(Directory.GetCurrentDirectory(), "functions", "test", "fixtures", "save_commit.json");

        [Test]
        public void SaveCommit_MatchesTheGoldenBodyVerifiedAgainstTheEmulator()
        {
            var commit = FirestoreDocuments.SaveCommit(DocumentsRoot, "uid-golden", SampleSave());
            var golden = JObject.Parse(File.ReadAllText(GoldenCommitPath));

            Assert.IsTrue(JToken.DeepEquals(golden, commit), commit.ToString());
        }

        [Test]
        public void Fields_RoundTripEveryJsonType()
        {
            var json = JObject.Parse("{\"text\":\"a\",\"count\":1789480000000,\"ratio\":0.5,\"flag\":true,\"nothing\":null,\"map\":{\"inner\":[1,\"two\",{\"x\":3}]},\"empty\":{}}");

            var restored = FirestoreDocuments.FromFields(FirestoreDocuments.ToFields(json));

            Assert.IsTrue(JToken.DeepEquals(json, restored), restored.ToString());
        }

        [Test]
        public void TryReadSave_RestoresTheSaveFromAFirestoreDocument()
        {
            Assert.IsTrue(FirestoreDocuments.TryReadSave(FirestoreSaveDocument(SampleSave()), out var save, out var error), error);

            Assert.AreEqual(Act.Two, save.ActFlag);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 255 }, save.FogBitmask);
            Assert.AreEqual(DamarisChoice.Accepted, save.DamarisBranch);
            Assert.AreEqual(new Coord(7, 3), save.Position);
            Assert.AreEqual(1789480000000, save.Timestamp);
            Assert.AreEqual(2, save.Inventory["heraclitus"]);
            Assert.AreEqual(DuelVictory.Maieutics, save.Duels["batilo"].Victory);
            Assert.AreEqual("yes", save.Flags["met_menexeno"]);
        }

        [Test]
        public void SaveCommit_WritesTheCompendiumAsAStringArrayThatReadsBack()
        {
            var original = SampleSave();
            original.Compendium.Add("straw_man");
            original.Compendium.Add("ad_hominem");

            var fields = FirestoreDocuments.SaveCommit(DocumentsRoot, "uid-1", original)["writes"][0]["update"]["fields"];
            var values = fields["compendium"]["arrayValue"]["values"];
            CollectionAssert.AreEqual(new[] { "ad_hominem", "straw_man" }, values.Select(value => (string)value["stringValue"]));

            Assert.IsTrue(FirestoreDocuments.TryReadSave(FirestoreSaveDocument(original), out var save, out var error), error);
            CollectionAssert.AreEquivalent(new[] { "ad_hominem", "straw_man" }, save.Compendium);
            Assert.IsTrue(FirestoreDocuments.TryReadSave(FirestoreSaveDocument(SampleSave()), out var legacy, out error), error);
            Assert.IsEmpty(legacy.Compendium);
        }

        [TestCase("{}")]
        [TestCase("not json")]
        [TestCase("{\"fields\":{\"actFlag\":{\"integerValue\":\"abc\"}}}")]
        [TestCase("{\"fields\":{\"actFlag\":{\"geoPointValue\":{}}}}")]
        [TestCase("{\"fields\":{\"actFlag\":{\"integerValue\":\"9\"}}}")]
        public void TryReadSave_RejectsMalformedDocuments(string document)
        {
            Assert.IsFalse(FirestoreDocuments.TryReadSave(document, out var save, out var error));
            Assert.IsNull(save);
            Assert.IsNotEmpty(error);
        }
    }
}
