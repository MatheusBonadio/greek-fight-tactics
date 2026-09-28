using System;
using System.Collections;
using Game.Core;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public sealed class GameAudioPlayTests
    {
        private const float StartTimeoutSeconds = 5f;
        private const float SettleSeconds = 2.5f;
        private const float ProgressSeconds = 0.5f;

        private IsolatedDataDirectory data;

        [UnitySetUp]
        public IEnumerator LoadTheMenuWithSound()
        {
            data = IsolatedDataDirectory.Enter();
            GameAudio.Muted = false;
            yield return SceneManager.LoadSceneAsync(PauseMenuController.DefaultMainMenuScene, LoadSceneMode.Single);
            yield return WaitFor(() => GameAudio.Active && GameAudio.Active.Library, StartTimeoutSeconds);
            Assert.IsTrue(GameAudio.Active && GameAudio.Active.Library, "the menu must create the audio player with its library");
            yield return WaitFor(() => GameAudio.Active.PlayingMusic == AudioIds.MusicTitle, StartTimeoutSeconds);
            Assert.AreEqual(AudioIds.MusicTitle, GameAudio.Active.PlayingMusic, "the menu starts the title music");
            GameAudio.Active.SetVolume(AudioBus.Music, 1f);
        }

        [UnityTearDown]
        public IEnumerator SilenceAgain()
        {
            GameAudio.Muted = true;
            GameAudio.Shutdown();
            data?.Dispose();
            data = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator AfterADuel_TheMapMusicComesBackLowerAndRisesInTheMap()
        {
            var audio = GameAudio.Active;
            var map = audio.Library.Get(AudioIds.MusicAgora);
            var battle = audio.Library.Get(AudioIds.MusicBattle);

            GameAudio.PlayMusic(AudioIds.MusicAgora);
            yield return new WaitForSecondsRealtime(SettleSeconds);
            yield return AssertAudible(audio, map, GameAudio.FullMusicLevel, "the map music before the duel");

            GameAudio.PlayMusic(AudioIds.MusicBattle, GameAudio.BattleCrossfadeSeconds);
            yield return new WaitForSecondsRealtime(SettleSeconds);
            yield return AssertAudible(audio, battle, GameAudio.FullMusicLevel, "the duel music");

            GameAudio.PlayMusic(AudioIds.MusicAgora, GameAudio.StingFadeSeconds, GameAudio.AfterDuelMusicLevel);
            yield return new WaitForSecondsRealtime(SettleSeconds);
            yield return AssertAudible(audio, map, GameAudio.AfterDuelMusicLevel, "the map music under the words after the duel");
            var afterDuel = audio.CurrentMusic;

            GameAudio.PlayMusic(AudioIds.MusicAgora);
            yield return new WaitForSecondsRealtime(SettleSeconds);
            Assert.AreSame(afterDuel, audio.CurrentMusic, "back in the map the same track keeps playing instead of starting over");
            yield return AssertAudible(audio, map, GameAudio.FullMusicLevel, "the map music back in the map");
        }

        private static IEnumerator AssertAudible(GameAudio audio, AudioClip clip, float level, string what)
        {
            var source = audio.CurrentMusic;
            Assert.AreSame(clip, source.clip, $"{what} is not the clip on the music source");
            Assert.IsTrue(source.isPlaying, $"{what} is not playing");
            Assert.AreEqual(level, source.volume, 0.01f, $"{what} did not settle at its level");
            var before = source.timeSamples;
            yield return new WaitForSecondsRealtime(ProgressSeconds);
            Assert.AreNotEqual(before, source.timeSamples, $"{what} is stuck at sample {before}");
            TestContext.WriteLine($"{what}: {clip.name} at {source.time:0.00}s, volume {source.volume:0.00}");
        }

        private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }
    }
}
