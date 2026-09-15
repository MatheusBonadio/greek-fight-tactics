using System.IO;
using System.Linq;
using Game.Content;
using Game.Core;
using Game.EditorTools;
using Game.Grid;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode.Content
{
    public sealed class FallacyCatalogTests
    {
        private const int CatalogSize = 20;
        private const int ParadoxCount = 5;

        private static readonly string[] InformalIds =
        {
            "ad_hominem", "straw_man", "false_dilemma", "appeal_to_authority", "appeal_to_ignorance",
            "hasty_generalization", "slippery_slope", "appeal_to_emotion", "equivocation", "begging_the_question"
        };

        private static readonly string[] FormalIds =
        {
            "affirming_the_consequent", "denying_the_antecedent", "undistributed_middle", "four_terms", "invalid_disjunctive_syllogism"
        };

        private static readonly string[] ParadoxIds =
        {
            "zeno_tortoise", "liar", "sorites", "ship_of_theseus", "flux_vs_permanence"
        };

        private FallacyCatalogSO catalog;

        [SetUp]
        public void SetUp()
        {
            catalog = AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath);
            Assert.IsNotNull(catalog, $"run DataTools.Regenerate to create {DataTools.CatalogPath}");
        }

        [Test]
        public void Catalog_HasEveryFallacyWithTheRightCategory()
        {
            Assert.AreEqual(CatalogSize, catalog.Count);
            AssertCategory(InformalIds, FallacyCategory.Informal);
            AssertCategory(FormalIds, FallacyCategory.Formal);
            AssertCategory(ParadoxIds, FallacyCategory.Paradox);
        }

        [Test]
        public void Paradoxes_AreTheOnlyEntriesThatCannotBeRefuted()
        {
            CollectionAssert.AreEquivalent(InformalIds.Concat(FormalIds), catalog.RefutableIds);
            Assert.AreEqual(ParadoxCount, catalog.All.Count(fallacy => !fallacy.IsRefutable));
        }

        [Test]
        public void EveryEntry_HasPortugueseTextAndASixteenPixelIcon()
        {
            foreach (var fallacy in catalog.All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(fallacy.DisplayName), fallacy.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(fallacy.Description), fallacy.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(fallacy.PlayerHint), fallacy.Id);
                Assert.IsNotNull(fallacy.Icon, fallacy.Id);
                Assert.AreEqual(new Rect(0, 0, 16, 16), fallacy.Icon.rect, fallacy.Id);
                Assert.AreEqual(PixelArtImporter.PixelsPerUnit, fallacy.Icon.pixelsPerUnit, fallacy.Id);
                Assert.AreEqual(FilterMode.Point, fallacy.Icon.texture.filterMode, fallacy.Id);
            }
        }

        [Test]
        public void Icons_AreDistinctPerFallacy()
        {
            var signatures = catalog.All
                .Select(fallacy => File.ReadAllBytes(AssetDatabase.GetAssetPath(fallacy.Icon)))
                .Select(bytes => string.Join(",", bytes))
                .ToList();

            Assert.AreEqual(CatalogSize, signatures.Distinct().Count());
        }

        [Test]
        public void ArgumentSchema_ListsExactlyTheRefutableFallaciesAndKnownTones()
        {
            var schema = JObject.Parse(File.ReadAllText(Path.Combine("docs", "argument.schema.json")));
            var schemaFallacies = schema["properties"]["fallacy_id"]["enum"]
                .Where(token => token.Type != JTokenType.Null)
                .Select(token => (string)token);
            var schemaTones = schema["properties"]["tone"]["enum"].Select(token => (string)token);

            CollectionAssert.AreEquivalent(catalog.RefutableIds, schemaFallacies);
            CollectionAssert.AreEquivalent(ArgumentTones.All, schemaTones);
            Assert.AreEqual(ArgumentValidator.MinSpeechLength, (int)schema["properties"]["speech"]["minLength"]);
            Assert.AreEqual(ArgumentValidator.MaxSpeechLength, (int)schema["properties"]["speech"]["maxLength"]);
        }

        [Test]
        public void TerrainCostTable_MarksWaterAndRiftImpassable()
        {
            var table = AssetDatabase.LoadAssetAtPath<TerrainCostTableSO>(DataTools.TerrainCostTablePath);

            Assert.IsNotNull(table);
            Assert.AreEqual(TileData.ImpassableCost, table.CostOf(TerrainId.Water));
            Assert.AreEqual(TileData.ImpassableCost, table.CostOf(TerrainId.Rift));
            Assert.AreEqual(1, table.CostOf(TerrainId.Marble));
            Assert.AreEqual(2, table.CostOf(TerrainId.Stone));
        }

        private void AssertCategory(string[] ids, FallacyCategory expected)
        {
            foreach (var id in ids)
            {
                Assert.IsTrue(catalog.TryGet(id, out var fallacy), id);
                Assert.AreEqual(expected, fallacy.Category, id);
                Assert.AreEqual(id, fallacy.Id);
            }
        }
    }
}
