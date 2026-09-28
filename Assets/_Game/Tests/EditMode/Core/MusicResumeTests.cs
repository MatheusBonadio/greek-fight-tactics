using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode.Core
{
    public sealed class MusicResumeTests
    {
        private const string Agora = "music_agora";
        private const float Length = 9.6f;

        [Test]
        public void ATrackNeverHeard_StartsFromTheTop()
        {
            Assert.AreEqual(0f, new MusicResume().StartFor(Agora, Length, 10f));
        }

        [Test]
        public void ComingBackSoon_PicksUpWhereTheTrackStopped()
        {
            var resume = new MusicResume();
            resume.Remember(Agora, 4.5f, 100f);

            Assert.AreEqual(4.5f, resume.StartFor(Agora, Length, 100f + MusicResume.WindowSeconds), 0.0001f,
                "a duel in the agora does not restart the agora's music");
        }

        [Test]
        public void ComingBackLate_StartsTheTrackAgain()
        {
            var resume = new MusicResume();
            resume.Remember(Agora, 4.5f, 100f);

            Assert.AreEqual(0f, resume.StartFor(Agora, Length, 100f + MusicResume.WindowSeconds + 1f));
        }

        [Test]
        public void APositionPastTheEnd_WrapsIntoTheLoop()
        {
            var resume = new MusicResume();
            resume.Remember(Agora, Length + 1f, 0f);

            Assert.AreEqual(1f, resume.StartFor(Agora, Length, 1f), 0.0001f);
        }

        [Test]
        public void EachTrack_KeepsItsOwnPlace()
        {
            var resume = new MusicResume();
            resume.Remember(Agora, 3f, 0f);
            resume.Remember("music_battle", 7f, 0f);

            Assert.AreEqual(3f, resume.StartFor(Agora, Length, 1f), 0.0001f);
            Assert.AreEqual(7f, resume.StartFor("music_battle", Length, 1f), 0.0001f);
            Assert.AreEqual(0f, resume.StartFor(null, Length, 1f));
            Assert.AreEqual(0f, resume.StartFor(Agora, 0f, 1f));
        }
    }
}
