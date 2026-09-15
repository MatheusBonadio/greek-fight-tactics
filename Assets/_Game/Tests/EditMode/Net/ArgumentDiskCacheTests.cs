using System.IO;
using System.Text;
using Game.Net;
using NUnit.Framework;

namespace Game.Tests.EditMode.Net
{
    public sealed class ArgumentDiskCacheTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), $"gft-cache-{System.Guid.NewGuid():N}");
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
        public void KeyFor_IsAStableHashOfTheRequest()
        {
            var first = ArgumentDiskCache.KeyFor("thrasybios", "ad_hominem", 7);

            Assert.AreEqual(first, ArgumentDiskCache.KeyFor("thrasybios", "ad_hominem", 7));
            Assert.AreNotEqual(first, ArgumentDiskCache.KeyFor("thrasybios", "ad_hominem", 8));
            Assert.AreEqual(32, first.Length);
            Assert.AreEqual("5ec6c70384b1a269847f092224354426", first);
        }

        [Test]
        public void PutThenGet_ReturnsTheStoredJson()
        {
            var cache = new ArgumentDiskCache(directory);

            Assert.IsTrue(cache.Put("a", "{\"speech\":\"um\"}"));

            Assert.IsTrue(cache.TryGet("a", out var json));
            Assert.AreEqual("{\"speech\":\"um\"}", json);
            Assert.IsFalse(cache.TryGet("missing", out _));
        }

        [Test]
        public void SizeLimit_EvictsTheLeastRecentlyUsedEntry()
        {
            var entry = new string('x', 40);
            var cache = new ArgumentDiskCache(directory, Encoding.UTF8.GetByteCount(entry) * 2);
            cache.Put("old", entry);
            cache.Put("recent", entry);
            cache.TryGet("old", out _);

            cache.Put("new", entry);

            Assert.IsTrue(cache.TryGet("old", out _));
            Assert.IsFalse(cache.TryGet("recent", out _));
            Assert.IsTrue(cache.TryGet("new", out _));
            Assert.LessOrEqual(cache.SizeBytes, Encoding.UTF8.GetByteCount(entry) * 2);
        }

        [Test]
        public void EntryLargerThanTheLimit_IsRefused()
        {
            var cache = new ArgumentDiskCache(directory, 10);

            Assert.IsFalse(cache.Put("big", new string('x', 11)));
            Assert.AreEqual(0, cache.Count);
        }

        [Test]
        public void SaveThenLoad_RestoresEntriesAndRecencyAndDropsEvictedFiles()
        {
            var entry = new string('y', 30);
            var limit = Encoding.UTF8.GetByteCount(entry) * 2;
            var cache = new ArgumentDiskCache(directory, limit);
            cache.Put("first", entry);
            cache.SaveAsync().Wait();
            cache.Put("second", entry);
            cache.Put("third", entry);
            cache.SaveAsync().Wait();

            Assert.IsFalse(File.Exists(Path.Combine(directory, "first.json")));

            var reloaded = new ArgumentDiskCache(directory, limit);
            Assert.AreEqual(2, reloaded.Load());
            Assert.IsTrue(reloaded.TryGet("second", out _));
            reloaded.Put("fourth", entry);

            Assert.IsFalse(reloaded.TryGet("third", out _));
            Assert.IsTrue(reloaded.TryGet("second", out _));
        }

        [Test]
        public void Load_WithoutADirectory_StartsEmpty()
        {
            Assert.AreEqual(0, new ArgumentDiskCache(directory).Load());
        }
    }
}
