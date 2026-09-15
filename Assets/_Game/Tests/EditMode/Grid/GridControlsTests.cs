using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Tests.EditMode.Grid
{
    public sealed class GridControlsTests
    {
        private const string AssetPath = "Assets/_Game/Settings/GridControls.inputactions";

        private InputActionAsset asset;

        [SetUp]
        public void SetUp()
        {
            asset = InputActionAsset.FromJson(File.ReadAllText(AssetPath));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(asset);
        }

        [Test]
        public void GridMap_HasPointerNavigationConfirmAndCancel()
        {
            var map = asset.FindActionMap("Grid", true);

            CollectionAssert.AreEquivalent(new[] { "Point", "Navigate", "Confirm", "Cancel" }, map.actions.Select(action => action.name));
            Assert.AreEqual(1, asset.actionMaps.Count);
        }

        [Test]
        public void Navigate_AcceptsWasdArrowsDpadAndStick()
        {
            var paths = PathsFor("Navigate");

            CollectionAssert.IsSupersetOf(paths, new[]
            {
                "<Keyboard>/w", "<Keyboard>/a", "<Keyboard>/s", "<Keyboard>/d",
                "<Keyboard>/upArrow", "<Keyboard>/downArrow", "<Keyboard>/leftArrow", "<Keyboard>/rightArrow",
                "<Gamepad>/dpad", "<Gamepad>/leftStick"
            });
        }

        [TestCase("Confirm")]
        [TestCase("Cancel")]
        public void ConfirmAndCancel_WorkOnMouseKeyboardAndGamepad(string actionName)
        {
            var devices = PathsFor(actionName).Select(path => path.Substring(1, path.IndexOf('>') - 1)).Distinct();

            CollectionAssert.IsSupersetOf(devices, new[] { "Mouse", "Keyboard", "Gamepad" });
        }

        [Test]
        public void Point_ReadsTheMousePosition()
        {
            CollectionAssert.Contains(PathsFor("Point"), "<Mouse>/position");
        }

        private string[] PathsFor(string actionName) =>
            asset.FindAction($"Grid/{actionName}", true).bindings
                .Where(binding => !binding.isComposite)
                .Select(binding => binding.path)
                .ToArray();
    }
}
