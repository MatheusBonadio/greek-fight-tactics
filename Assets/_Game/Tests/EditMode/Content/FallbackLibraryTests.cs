using System.Collections.Generic;
using System.Linq;
using Game.Content;
using Game.Core;
using Game.EditorTools;
using Game.Net;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode.Content
{
    public sealed class FallbackLibraryTests
    {
        private const int Seed = 402;
        private const int MinimumTotalArguments = 60;
        private const int Draws = 200;

        private FallacyCatalogSO catalog;
        private FallbackLibrarySO library;
        private ArgumentValidator validator;

        [SetUp]
        public void SetUp()
        {
            catalog = AssetDatabase.LoadAssetAtPath<FallacyCatalogSO>(DataTools.CatalogPath);
            library = AssetDatabase.LoadAssetAtPath<FallbackLibrarySO>(DataTools.FallbackLibraryPath);
            Assert.IsNotNull(catalog, $"run DataTools.Regenerate to create {DataTools.CatalogPath}");
            Assert.IsNotNull(library, $"run DataTools.Regenerate to create {DataTools.FallbackLibraryPath}");
            validator = new ArgumentValidator(catalog.RefutableIds);
        }

        [Test]
        public void Library_CoversExactlyTheCatalog()
        {
            CollectionAssert.AreEquivalent(catalog.All.Select(fallacy => fallacy.Id), library.EntryIds);
        }

        [Test]
        public void EveryEntry_HasAtLeastThreeArgumentsAndNoneIsRejected()
        {
            var total = 0;
            foreach (var fallacy in catalog.All)
            {
                var arguments = Load(fallacy.Id, out var declared);

                Assert.AreEqual(declared, arguments.Count, $"{fallacy.Id} has arguments that fail validation");
                Assert.GreaterOrEqual(arguments.Count, DataTools.MinimumFallbackArguments, fallacy.Id);
                total += arguments.Count;
            }

            Assert.GreaterOrEqual(total, MinimumTotalArguments);
        }

        [Test]
        public void Arguments_MatchTheirEntryAndTheOpponentProfileTone()
        {
            foreach (var fallacy in catalog.All)
            {
                var expectedTones = ArgumentTones.For(fallacy.IsRefutable ? Profile.Sophist : Profile.Philosopher);
                foreach (var argument in Load(fallacy.Id, out _))
                {
                    Assert.AreEqual(fallacy.IsRefutable ? fallacy.Id : null, argument.FallacyId, fallacy.Id);
                    Assert.AreEqual(fallacy.IsRefutable, argument.IsRefutable, fallacy.Id);
                    CollectionAssert.Contains(expectedTones, argument.Tone, $"{fallacy.Id}: {argument.Speech}");
                }
            }
        }

        [Test]
        public void FallbackArgumentLibrary_ServesEveryEntryWithoutImmediateRepetition()
        {
            var fallback = new FallbackArgumentLibrary(library, validator, Seed);

            foreach (var fallacy in catalog.All)
            {
                Assert.GreaterOrEqual(fallback.CountFor(fallacy.Id), DataTools.MinimumFallbackArguments, fallacy.Id);

                string previous = null;
                for (var draw = 0; draw < Draws; draw++)
                {
                    Assert.IsTrue(fallback.TryNext(fallacy.Id, out var argument), fallacy.Id);
                    Assert.AreNotEqual(previous, argument.Speech, $"{fallacy.Id} repeated on draw {draw}");
                    previous = argument.Speech;
                }
            }
        }

        [Test]
        public void FallbackArgumentLibrary_UnknownEntry_ReturnsFalse()
        {
            var fallback = new FallbackArgumentLibrary(library, validator, Seed);

            Assert.IsFalse(fallback.TryNext("not_in_catalog", out _));
            Assert.IsFalse(fallback.TryNext(null, out _));
            Assert.AreEqual(0, fallback.CountFor("not_in_catalog"));
        }

        private List<Argument> Load(string entryId, out int declared)
        {
            Assert.IsTrue(library.TryGetJson(entryId, out var json), entryId);
            declared = JArray.Parse(json).Count;
            var arguments = new List<Argument>();
            ArgumentJson.ParseValidList(json, validator, arguments);
            return arguments;
        }
    }
}
