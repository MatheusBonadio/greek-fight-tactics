using Game.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Tests.EditMode.Build
{
    public sealed class RenderSetupTests
    {
        [Test]
        public void CameraRigPrefab_UsesPixelPerfectSettingsFromTheArtSpec()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RenderSetupTools.CameraRigPrefabPath);
            Assert.IsNotNull(prefab, $"run RenderSetupTools.Apply to create {RenderSetupTools.CameraRigPrefabPath}");

            var camera = prefab.GetComponent<Camera>();
            var pixelPerfect = prefab.GetComponent<PixelPerfectCamera>();

            Assert.IsNotNull(camera);
            Assert.IsTrue(camera.orthographic);
            Assert.IsNotNull(pixelPerfect);
            Assert.AreEqual(16, pixelPerfect.assetsPPU);
            Assert.AreEqual(224, pixelPerfect.refResolutionX);
            Assert.AreEqual(224, pixelPerfect.refResolutionY);
            Assert.AreEqual(PixelPerfectCamera.GridSnapping.UpscaleRenderTexture, pixelPerfect.gridSnapping);
            Assert.AreEqual(PixelPerfectCamera.CropFrame.None, pixelPerfect.cropFrame);
        }

        [Test]
        public void Renderer2D_SortsTransparencyAlongTheVerticalAxis()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableObject>(RenderSetupTools.Renderer2DPath);
            Assert.IsNotNull(renderer);

            var serialized = new SerializedObject(renderer);

            Assert.AreEqual((int)TransparencySortMode.CustomAxis, serialized.FindProperty("m_TransparencySortMode").intValue);
            Assert.AreEqual(RenderSetupTools.TransparencySortAxis, serialized.FindProperty("m_TransparencySortAxis").vector3Value);
        }
    }
}
