using System;
using System.IO;
using System.Linq;
using Game.Content;
using Game.Core;
using Game.EditorTools;
using Game.Net;
using Game.UI;
using NUnit.Framework;
using UnityEditor;
using static Game.Tests.EditMode.Net.NetSamples;

namespace Game.Tests.EditMode.Net
{
    public sealed class SaveSlotTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), $"gft-slots-{Guid.NewGuid():N}");
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
        public void SaveJson_RoundTripsLocationAndPlaytime()
        {
            Assert.IsTrue(SaveJson.TryDeserialize(SaveJson.Serialize(SampleSave()), out var save, out var error), error);

            Assert.AreEqual("agora", save.Location);
            Assert.AreEqual(3725, save.PlaytimeSeconds);
        }

        [Test]
        public void SaveJson_AcceptsOlderSavesWithoutTheNewFields()
        {
            Assert.IsTrue(SaveJson.TryDeserialize("{\"actFlag\":2,\"timestamp\":5}", out var save, out var error), error);

            Assert.AreEqual(string.Empty, save.Location);
            Assert.AreEqual(0, save.PlaytimeSeconds);
            Assert.IsFalse(SaveJson.TryDeserialize("{\"playtimeSeconds\":-1}", out _, out var negativeError));
            StringAssert.Contains("playtimeSeconds", negativeError);
        }

        [Test]
        public void SlotNames_CoverThreeSlotsStartingAtTheLegacyDefault()
        {
            Assert.AreEqual(LocalSaveStore.DefaultSlot, SaveSlots.NameFor(SaveSlots.DefaultIndex));
            Assert.AreEqual("slot2", SaveSlots.NameFor(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => SaveSlots.NameFor(SaveSlots.Count));
        }

        [Test]
        public void Catalog_SummarizesEmptyUsableAndCorruptSlots()
        {
            var catalog = new SaveSlotCatalog(directory);
            Assert.IsFalse(catalog.AnyUsable());

            catalog.StoreFor(0).Save(SampleSave());
            Directory.CreateDirectory(directory);
            File.WriteAllText(catalog.StoreFor(2).FilePath, "{ not json");

            var slots = catalog.ReadAll();

            Assert.AreEqual(SaveSlots.Count, slots.Count);
            Assert.IsTrue(slots[0].IsUsable);
            Assert.AreEqual(Act.Two, slots[0].Act);
            Assert.AreEqual("agora", slots[0].Location);
            Assert.AreEqual(3725, slots[0].PlaytimeSeconds);
            Assert.IsFalse(slots[1].Exists);
            Assert.IsTrue(slots[2].IsCorrupt);
            Assert.IsTrue(catalog.AnyUsable());
        }

        [Test]
        public void Catalog_RemembersTheActiveSlotAndIgnoresGarbage()
        {
            var catalog = new SaveSlotCatalog(directory);
            Assert.AreEqual(SaveSlots.DefaultIndex, catalog.ReadActiveSlot());

            catalog.WriteActiveSlot(2);
            Assert.AreEqual(2, new SaveSlotCatalog(directory).ReadActiveSlot());

            File.WriteAllText(catalog.ActiveSlotPath, "7");
            Assert.AreEqual(SaveSlots.DefaultIndex, catalog.ReadActiveSlot());
            Assert.Throws<ArgumentOutOfRangeException>(() => catalog.WriteActiveSlot(-1));
        }

        [Test]
        public void SlotsModel_LoadSkipsUnusableSlots()
        {
            var model = new SaveSlotsModel(SaveSlotsMode.Load, new[] { SaveSlotSummary.Empty(0), SaveSlotSummary.From(1, SampleSave()), SaveSlotSummary.Corrupt(2) });

            Assert.AreEqual(1, model.Cursor);
            model.Move(1);
            Assert.AreEqual(1, model.Cursor);
            model.Point(0);
            Assert.AreEqual(1, model.Cursor);

            Assert.IsTrue(model.TryConfirm(out var slot));
            Assert.AreEqual(1, slot);
        }

        [Test]
        public void SlotsModel_SaveAsksBeforeOverwritingAndDefaultsToNo()
        {
            var model = new SaveSlotsModel(SaveSlotsMode.Save, new[] { SaveSlotSummary.From(0, SampleSave()), SaveSlotSummary.Empty(1), SaveSlotSummary.Empty(2) });

            Assert.IsFalse(model.TryConfirm(out _));
            Assert.AreEqual(SaveSlotsStep.ConfirmingOverwrite, model.Step);
            Assert.AreEqual(SaveSlotsModel.ConfirmNo, model.ConfirmCursor);

            Assert.IsFalse(model.TryConfirm(out _));
            Assert.AreEqual(SaveSlotsStep.Choosing, model.Step);

            model.TryConfirm(out _);
            model.Move(1);
            Assert.IsTrue(model.TryConfirm(out var overwritten));
            Assert.AreEqual(0, overwritten);

            model.TryConfirm(out _);
            Assert.IsTrue(model.Cancel());
            Assert.IsFalse(model.Cancel());

            model.Point(1);
            Assert.IsTrue(model.TryConfirm(out var empty));
            Assert.AreEqual(1, empty);
        }

        [Test]
        public void SlotText_SummarizesActLocationAndPlaytimeFromData()
        {
            var uiText = AssetDatabase.LoadAssetAtPath<UiTextSO>(DataTools.UiTextPath);
            Assert.IsNotNull(uiText);

            var line = SaveSlotText.Line(SaveSlotSummary.From(0, SampleSave()), uiText);

            StringAssert.Contains(string.Format(uiText.Get(SaveSlotText.SlotKey), 1), line);
            StringAssert.Contains(uiText.Get(SaveSlotText.ActKey(Act.Two)), line);
            StringAssert.Contains(uiText.Get("location.agora"), line);
            StringAssert.Contains(string.Format(uiText.Get(SaveSlotText.PlaytimeKey), 1, 2), line);
            StringAssert.StartsWith(string.Format(uiText.Get(SaveSlotText.SlotKey), 2), SaveSlotText.Line(SaveSlotSummary.Empty(1), uiText));

            foreach (var key in new[] { SaveSlotText.TitleSaveKey, SaveSlotText.TitleLoadKey, SaveSlotText.EmptyLineKey, SaveSlotText.CorruptLineKey, SaveSlotText.SummaryLineKey, SaveSlotText.ConfirmOverwriteKey, SaveSlotText.YesKey, SaveSlotText.NoKey, SaveSlotText.LocationKey(null) }
                .Concat(Enum.GetValues(typeof(Act)).Cast<Act>().Select(SaveSlotText.ActKey)))
            {
                Assert.IsTrue(uiText.Contains(key), key);
            }
        }
    }
}
