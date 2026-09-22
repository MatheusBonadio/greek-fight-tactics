using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Scripting.LifecycleManagement;

namespace Game.Net
{
    public sealed class ArgumentDiskCache
    {
        public const long DefaultMaxBytes = 5L * 1024 * 1024;
        public const string IndexFileName = "index.json";

        private const string EntryExtension = ".json";
        private const int KeyBytes = 16;

        [NoAutoStaticsCleanup]
        private static readonly Dictionary<string, Task> PendingWrites = new(StringComparer.OrdinalIgnoreCase);

        private readonly string directory;
        private readonly long maxBytes;
        private readonly object gate = new();
        private readonly LinkedList<string> recency = new();
        private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
        private readonly HashSet<string> dirty = new(StringComparer.Ordinal);
        private readonly HashSet<string> evicted = new(StringComparer.Ordinal);
        private long sizeBytes;

        public ArgumentDiskCache(string directory, long maxBytes = DefaultMaxBytes)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("Cache directory is required.", nameof(directory));
            }

            if (maxBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBytes), maxBytes, "Cache size limit must be positive.");
            }

            this.directory = directory;
            this.maxBytes = maxBytes;
        }

        public int Count
        {
            get
            {
                lock (gate)
                {
                    return entries.Count;
                }
            }
        }

        public long SizeBytes
        {
            get
            {
                lock (gate)
                {
                    return sizeBytes;
                }
            }
        }

        public static string KeyFor(string opponentId, string fallacyId, int seed)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes($"{opponentId}|{fallacyId}|{seed}"));
            return BitConverter.ToString(hash, 0, KeyBytes).Replace("-", string.Empty).ToLowerInvariant();
        }

        public bool TryGet(string key, out string json)
        {
            lock (gate)
            {
                if (key == null || !entries.TryGetValue(key, out var entry))
                {
                    json = null;
                    return false;
                }

                recency.Remove(entry.Node);
                recency.AddFirst(entry.Node);
                json = entry.Json;
                return true;
            }
        }

        public bool Put(string key, string json)
        {
            if (key == null || json == null)
            {
                return false;
            }

            var size = ByteCount(json);
            if (size > maxBytes)
            {
                return false;
            }

            lock (gate)
            {
                if (entries.TryGetValue(key, out var existing))
                {
                    sizeBytes -= existing.Size;
                    recency.Remove(existing.Node);
                }

                entries[key] = new Entry(json, size, recency.AddFirst(key));
                sizeBytes += size;
                dirty.Add(key);
                evicted.Remove(key);
                EvictToFit();
                return true;
            }
        }

        public int Load()
        {
            WaitForPendingWrites();
            if (!Directory.Exists(directory))
            {
                return 0;
            }

            var indexPath = Path.Combine(directory, IndexFileName);
            var order = File.Exists(indexPath)
                ? JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(indexPath)) ?? new List<string>()
                : new List<string>();

            lock (gate)
            {
                recency.Clear();
                entries.Clear();
                dirty.Clear();
                evicted.Clear();
                sizeBytes = 0;

                foreach (var key in Enumerable.Reverse(order))
                {
                    var path = EntryPath(key);
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    var json = File.ReadAllText(path);
                    var size = ByteCount(json);
                    entries[key] = new Entry(json, size, recency.AddFirst(key));
                    sizeBytes += size;
                }

                EvictToFit();
                return entries.Count;
            }
        }

        public Task SaveAsync()
        {
            List<KeyValuePair<string, string>> toWrite;
            List<string> toDelete;
            List<string> order;

            lock (gate)
            {
                toWrite = dirty.Where(entries.ContainsKey).Select(key => new KeyValuePair<string, string>(key, entries[key].Json)).ToList();
                toDelete = evicted.ToList();
                order = recency.ToList();
                dirty.Clear();
                evicted.Clear();
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            Persist(toWrite, toDelete, order);
            return Task.CompletedTask;
#else
            return EnqueueWrite(() => Persist(toWrite, toDelete, order));
#endif
        }

        private Task EnqueueWrite(Action write)
        {
            lock (PendingWrites)
            {
                var folder = Path.GetFullPath(directory);
                var previous = PendingWrites.TryGetValue(folder, out var pending) ? pending : Task.CompletedTask;
                var next = previous.ContinueWith(_ => write(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
                PendingWrites[folder] = next;
                return next;
            }
        }

        private void WaitForPendingWrites()
        {
            Task pending;
            lock (PendingWrites)
            {
                if (!PendingWrites.TryGetValue(Path.GetFullPath(directory), out pending))
                {
                    return;
                }
            }

            Task.WhenAny(pending).Wait();
        }

        private void Persist(List<KeyValuePair<string, string>> toWrite, List<string> toDelete, List<string> order)
        {
            Directory.CreateDirectory(directory);
            foreach (var pair in toWrite)
            {
                WriteAtomically(EntryPath(pair.Key), pair.Value);
            }

            foreach (var key in toDelete)
            {
                var path = EntryPath(key);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }

            WriteAtomically(Path.Combine(directory, IndexFileName), JsonConvert.SerializeObject(order));
        }

        private void EvictToFit()
        {
            while (sizeBytes > maxBytes && recency.Last != null)
            {
                var key = recency.Last.Value;
                recency.RemoveLast();
                sizeBytes -= entries[key].Size;
                entries.Remove(key);
                dirty.Remove(key);
                evicted.Add(key);
            }
        }

        private string EntryPath(string key) => Path.Combine(directory, key + EntryExtension);

        private static void WriteAtomically(string path, string contents)
        {
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, contents);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(temporary, path);
        }

        private static long ByteCount(string json) => Encoding.UTF8.GetByteCount(json);

        private readonly struct Entry
        {
            public readonly string Json;
            public readonly long Size;
            public readonly LinkedListNode<string> Node;

            public Entry(string json, long size, LinkedListNode<string> node)
            {
                Json = json;
                Size = size;
                Node = node;
            }
        }
    }
}
