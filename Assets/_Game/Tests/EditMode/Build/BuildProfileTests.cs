using Game.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Profile;

namespace Game.Tests.EditMode.Build
{
    public sealed class BuildProfileTests
    {
        [TestCase(BuildProfileTools.WindowsDevelopmentName, true)]
        [TestCase(BuildProfileTools.WindowsReleaseName, false)]
        [TestCase(BuildProfileTools.WebName, false)]
        public void Profile_ExistsWithTheExpectedDevelopmentFlag(string profileName, bool expectedDevelopment)
        {
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(BuildProfileTools.PathFor(profileName));

            Assert.IsNotNull(profile, $"run BuildProfileTools.CreateProfiles and ConfigureProfiles to create {profileName}");
            Assert.IsTrue(BuildProfileTools.TryReadDevelopmentFlag(profile, out var development), profileName);
            Assert.AreEqual(expectedDevelopment, development, profileName);
        }
    }
}
