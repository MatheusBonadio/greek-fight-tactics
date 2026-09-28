using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Content;
using Game.Core;
using Game.EditorTools;
using Game.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode.Content
{
    public sealed class AudioLibraryTests
    {
        private AudioLibrarySO library;

        [SetUp]
        public void SetUp()
        {
            library = AssetDatabase.LoadAssetAtPath<AudioLibrarySO>(DataTools.AudioLibraryPath);
            Assert.IsNotNull(library, $"no audio library at {DataTools.AudioLibraryPath}");
        }

        [Test]
        public void EveryIdTheCodeAsksFor_HasAClip()
        {
            foreach (var id in new[]
                     {
                         AudioIds.MusicTitle, AudioIds.MusicKerameikos, AudioIds.MusicAgora, AudioIds.MusicArchive,
                         AudioIds.MusicAcademy, AudioIds.MusicBattleSophist, AudioIds.MusicBattlePhilosopher,
                         AudioIds.Cursor, AudioIds.Confirm, AudioIds.Cancel, AudioIds.Refute, AudioIds.Maieutics,
                         AudioIds.Damage, AudioIds.Counter, AudioIds.Victory, AudioIds.Defeat, AudioIds.VoiceBlip
                     })
            {
                Assert.IsNotNull(library.Get(id), id);
            }
        }

        [Test]
        public void EveryMapAndEveryPhase_HasTheMusicItNames()
        {
            foreach (var location in new[]
                     {
                         SandboxTools.AgoraLocationId, SandboxTools.KerameikosLocationId,
                         SandboxTools.ArchiveLocationId, SandboxTools.AcademyLocationId
                     })
            {
                Assert.IsNotNull(library.Get(AudioIds.MusicForLocation(location)), location);
            }

            foreach (var opponent in AssetDatabase.FindAssets($"t:{nameof(OpponentSO)}")
                         .Select(guid => AssetDatabase.LoadAssetAtPath<OpponentSO>(AssetDatabase.GUIDToAssetPath(guid)))
                         .Where(opponent => opponent != null && opponent.IsPhased))
            {
                foreach (var phase in opponent.Phases.Phases.Where(phase => !string.IsNullOrEmpty(phase.Music)))
                {
                    Assert.IsNotNull(library.Get(AudioIds.MusicForPhase(phase.Music)), $"{opponent.Id} {phase.Music}");
                }
            }
        }

        [Test]
        public void EverySpeaker_HasItsOwnVoicePitch()
        {
            var speakers = DataTools.LoadDialogueSource(DataTools.DialogueSourcePath).Speakers.Keys.ToList();
            var pitches = new Dictionary<string, float>();
            foreach (var speaker in speakers)
            {
                var pitch = library.PitchFor(speaker);
                Assert.GreaterOrEqual(pitch, GameAudio.MinimumPitch, speaker);
                Assert.LessOrEqual(pitch, GameAudio.MaximumPitch, speaker);
                pitches[speaker] = pitch;
            }

            Assert.AreEqual(speakers.Count, pitches.Count);
            Assert.Greater(pitches.Values.Distinct().Count(), speakers.Count / 2, "the cast sounds the same");
            Assert.AreEqual(AudioLibrarySO.DefaultPitch, library.PitchFor("nobody"));
        }

        [Test]
        public void EveryClip_ComesFromAnImportedFile()
        {
            foreach (var id in library.Ids)
            {
                var clip = library.Get(id);
                var path = AssetDatabase.GetAssetPath(clip);
                Assert.IsTrue(path.StartsWith(AudioTools.PlaceholderFolder + "/") || path.StartsWith(AudioTools.MusicFolder + "/"), $"{id}: {path}");
                Assert.IsTrue(File.Exists(path), path);
                Assert.Greater(clip.length, 0f, id);
            }
        }

        [Test]
        public void Effects_AreMonoInMemory_AndMusic_IsRecordedStereoAndStreamed()
        {
            foreach (var id in library.Ids)
            {
                var clip = library.Get(id);
                var path = AssetDatabase.GetAssetPath(clip);
                var importer = (UnityEditor.AudioImporter)AssetImporter.GetAtPath(path);
                var settings = importer.defaultSampleSettings;
                if (AudioIds.BusFor(id) == AudioBus.Music)
                {
                    StringAssert.StartsWith(AudioTools.MusicFolder + "/", path, id);
                    Assert.IsFalse(importer.forceToMono, id);
                    Assert.AreEqual(2, clip.channels, id);
                    Assert.AreEqual(AudioClipLoadType.Streaming, settings.loadType, id);
                    Assert.AreEqual(AudioCompressionFormat.Vorbis, settings.compressionFormat, id);
                }
                else
                {
                    StringAssert.StartsWith(AudioTools.PlaceholderFolder + "/", path, id);
                    Assert.AreEqual(1, clip.channels, id);
                    Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, settings.loadType, id);
                }
            }
        }

        [Test]
        public void PlacesThatShareATrack_ShareTheSameClip()
        {
            Assert.AreSame(library.Get(AudioIds.MusicAgora), library.Get(AudioIds.MusicArchive), "the archive stands in the agora");
            Assert.AreSame(library.Get(AudioIds.MusicBattle), library.Get(AudioIds.MusicFor(Profile.Sophist)));
            Assert.AreSame(library.Get(AudioIds.MusicBattle), library.Get(AudioIds.MusicFor(Profile.Philosopher)));
            Assert.AreNotSame(library.Get(AudioIds.MusicAgora), library.Get(AudioIds.MusicBattle));
            Assert.AreNotSame(library.Get(AudioIds.MusicTitle), library.Get(AudioIds.MusicKerameikos));
        }

        [Test]
        public void TheDuelThemeFollowsTheProfileAndTheBusFollowsTheId()
        {
            Assert.AreEqual(AudioIds.MusicBattleSophist, AudioIds.MusicFor(Profile.Sophist));
            Assert.AreEqual(AudioIds.MusicBattlePhilosopher, AudioIds.MusicFor(Profile.Philosopher));
            Assert.AreEqual(AudioBus.Music, AudioIds.BusFor(AudioIds.MusicAgora));
            Assert.AreEqual(AudioBus.Sfx, AudioIds.BusFor(AudioIds.Refute));
            Assert.AreEqual(AudioIds.Refute, AudioIds.SoundFor(ActionId.Refute));
            Assert.AreEqual(AudioIds.Maieutics, AudioIds.SoundFor(ActionId.Maieutics));
            Assert.IsNull(AudioIds.SoundFor(ActionId.Circulate));
            Assert.IsNull(AudioIds.MusicForLocation(null));
            Assert.IsNull(AudioIds.MusicForPhase(string.Empty));
        }
    }
}
