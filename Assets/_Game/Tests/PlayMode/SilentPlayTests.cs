using Game.UI;
using NUnit.Framework;

namespace Game.Tests.PlayMode
{
    [SetUpFixture]
    public sealed class SilentPlayTests
    {
        [OneTimeSetUp]
        public void Mute()
        {
            GameAudio.Muted = true;
        }

        [OneTimeTearDown]
        public void Unmute()
        {
            GameAudio.Muted = false;
        }
    }
}
