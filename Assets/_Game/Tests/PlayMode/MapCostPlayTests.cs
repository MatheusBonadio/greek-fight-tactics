using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Grid;
using Game.UI;
using NUnit.Framework;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public sealed class MapCostPlayTests
    {
        private const int WarmupFrames = 90;
        private const int SampledFrames = 240;
        private const float FrameBudgetMilliseconds = 16.7f;
        private const float LoadBudgetSeconds = 5f;
        private const string TilemapModule = "Tilemap";

        private static readonly string[] RenderCounters =
        {
            "Draw Calls Count",
            "Batches Count",
            "SetPass Calls Count",
            "Triangles Count",
        };

        private IsolatedDataDirectory data;

        [SetUp]
        public void IsolateData()
        {
            data = IsolatedDataDirectory.Enter();
        }

        [TearDown]
        public void ReleaseDataDirectory()
        {
            data?.Dispose();
            data = null;
        }

        [UnityTest]
        public IEnumerator Agora_DrawsAFullyRevealedMapInsideTheFrameBudget()
        {
            yield return Measure(LocationScenes.AgoraScene);
        }

        [UnityTest]
        public IEnumerator Workshop_DrawsAFullyRevealedQuarterInsideTheFrameBudget()
        {
            yield return Measure(LocationScenes.KerameikosScene);
        }

        private static IEnumerator Measure(string sceneName)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            yield return null;
            clock.Stop();
            var loadSeconds = clock.Elapsed.TotalSeconds;

            var controller = Object.FindAnyObjectByType<GridSandboxController>();
            Assert.IsNotNull(controller, $"{sceneName} must contain a GridSandboxController");
            RevealEverything(controller.Grid);
            yield return null;

            for (var frame = 0; frame < WarmupFrames; frame++)
            {
                yield return null;
            }

            var recorders = StartRecorders();
            var samples = new List<float>(SampledFrames);
            var peaks = new Dictionary<string, long>();
            for (var frame = 0; frame < SampledFrames; frame++)
            {
                yield return null;
                samples.Add(Time.unscaledDeltaTime * 1000f);
                foreach (var entry in recorders)
                {
                    peaks[entry.Key] = System.Math.Max(peaks.GetValueOrDefault(entry.Key), entry.Value.LastValue);
                }
            }

            foreach (var recorder in recorders.Values)
            {
                recorder.Dispose();
            }

            samples.Sort();
            var median = samples[samples.Count / 2];
            var worst = samples[(int)(samples.Count * 0.95f)];
            var cells = controller.Grid.Width * controller.Grid.Height;
            var counters = string.Join(", ", peaks.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key} {entry.Value}"));
            Debug.Log($"{sceneName}: {controller.Grid.Width}x{controller.Grid.Height} = {cells} cells, " +
                      $"load {loadSeconds:F2} s, median {median:F2} ms, 95th {worst:F2} ms, peak {counters}");

            Assert.Less(median, FrameBudgetMilliseconds,
                $"{sceneName} draws a fully revealed map at {median:F2} ms a frame, past the 60 fps budget");
            Assert.Less(loadSeconds, LoadBudgetSeconds,
                $"{sceneName} takes {loadSeconds:F2} s to open, long enough for the player to feel the wait");
        }

        private static Dictionary<string, ProfilerRecorder> StartRecorders()
        {
            var recorders = new Dictionary<string, ProfilerRecorder>();
            foreach (var counter in RenderCounters)
            {
                var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, counter);
                if (recorder.Valid)
                {
                    recorders[counter] = recorder;
                }
                else
                {
                    recorder.Dispose();
                }
            }

            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var handle in handles)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                if (!description.Name.Contains(TilemapModule) || recorders.ContainsKey(description.Name))
                {
                    continue;
                }

                var recorder = new ProfilerRecorder(handle, 1, ProfilerRecorderOptions.Default);
                recorder.Start();
                recorders[description.Name] = recorder;
            }

            return recorders;
        }

        private static void RevealEverything(GridModel grid)
        {
            var fog = new FogOfWar(grid);
            foreach (var cell in Enumerable.Range(0, grid.Width)
                         .SelectMany(x => Enumerable.Range(0, grid.Height).Select(y => new Coord(x, y))))
            {
                fog.Reveal(cell, 0);
            }
        }
    }
}
