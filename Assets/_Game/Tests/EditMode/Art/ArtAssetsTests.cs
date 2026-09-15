using System.IO;
using System.Linq;
using Game.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Tests.EditMode.Art
{
    public sealed class ArtAssetsTests
    {
        private const int PlaceholderPngCount = 39;
        private const int SpritesPerSheet = 16;
        private const float PivotTolerance = 0.0001f;

        [Test]
        public void Placeholders_AreCopiedIntoTheProject()
        {
            var sourceCount = Directory.GetFiles(ArtTools.SourceFolder, "*.png", SearchOption.AllDirectories).Length;
            var copiedCount = Directory.GetFiles(ArtTools.PlaceholderFolder, "*.png", SearchOption.AllDirectories).Length;

            Assert.AreEqual(PlaceholderPngCount, sourceCount);
            Assert.AreEqual(sourceCount, copiedCount);
        }

        [TestCaseSource(nameof(Characters))]
        public void CharacterSheet_IsSlicedIntoCellsWithFeetPivot(string character)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(ArtTools.SheetPathFor(character)).OfType<Sprite>().ToList();

            Assert.AreEqual(SpritesPerSheet, sprites.Count, character);
            foreach (var sprite in sprites)
            {
                Assert.AreEqual(ArtTools.CellWidth, sprite.rect.width, sprite.name);
                Assert.AreEqual(ArtTools.CellHeight, sprite.rect.height, sprite.name);
                var pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
                Assert.AreEqual(ArtTools.CharacterPivot.x, pivot.x, PivotTolerance, sprite.name);
                Assert.AreEqual(ArtTools.CharacterPivot.y, pivot.y, PivotTolerance, sprite.name);
                Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode, sprite.name);
            }
        }

        [Test]
        public void Tiles_UseBottomLeftPivot()
        {
            var tilePaths = Directory.GetFiles(ArtTools.TileFolder, "*.png", SearchOption.AllDirectories);
            Assert.IsNotEmpty(tilePaths);

            foreach (var path in tilePaths.Select(file => file.Replace('\\', '/')))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.IsNotNull(sprite, path);
                Assert.AreEqual(Vector2.zero, sprite.pivot, path);
            }
        }

        [TestCaseSource(nameof(Characters))]
        public void AnimatorController_BlendsIdleWalkAndActionByDirection(string character)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ArtTools.ControllerPathFor(character));
            Assert.IsNotNull(controller, character);

            CollectionAssert.IsSupersetOf(controller.parameters.Select(parameter => parameter.name), new[] { ArtTools.DirectionXParameter, ArtTools.DirectionYParameter });

            var states = controller.layers[0].stateMachine.states.Select(child => child.state).ToList();
            CollectionAssert.AreEquivalent(ArtTools.StateNames, states.Select(state => state.name));

            foreach (var state in states)
            {
                var tree = state.motion as BlendTree;
                Assert.IsNotNull(tree, $"{character}/{state.name}");
                Assert.AreEqual(BlendTreeType.SimpleDirectional2D, tree.blendType);
                Assert.AreEqual(ArtTools.DirectionXParameter, tree.blendParameter);
                Assert.AreEqual(ArtTools.DirectionYParameter, tree.blendParameterY);
                Assert.AreEqual(4, tree.children.Length, $"{character}/{state.name}");
            }
        }

        [TestCaseSource(nameof(Characters))]
        public void AnimationClips_OnlySwapSpritesWithoutInterpolation(string character)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ArtTools.ControllerPathFor(character));
            Assert.IsNotNull(controller, character);

            foreach (var clip in controller.animationClips)
            {
                Assert.IsEmpty(AnimationUtility.GetCurveBindings(clip), $"{clip.name} has interpolated float curves");
                var spriteBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                Assert.AreEqual(1, spriteBindings.Length, clip.name);
                Assert.AreEqual("m_Sprite", spriteBindings[0].propertyName, clip.name);
                Assert.IsNotEmpty(AnimationUtility.GetObjectReferenceCurve(clip, spriteBindings[0]), clip.name);
            }
        }

        private static string[] Characters() => ArtTools.Characters;
    }
}
