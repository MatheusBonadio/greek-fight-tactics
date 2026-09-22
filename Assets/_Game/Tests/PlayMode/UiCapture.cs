using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public static class UiCapture
    {
        public const int Width = 1920;
        public const int Height = 1080;
        public const int WorldZoom = 4;
        public const float ArtPixelsPerUnit = 16f;
        public const float OrthographicSize = Height / (2f * ArtPixelsPerUnit * WorldZoom);
        public const int OverlayOrder = 30000;

        public static IEnumerator Screen(string capturePath)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { filterMode = FilterMode.Point };
            var captureCamera = new GameObject("ui capture camera").AddComponent<Camera>();
            captureCamera.CopyFrom(Camera.main);
            captureCamera.orthographic = true;
            captureCamera.orthographicSize = OrthographicSize;
            captureCamera.targetTexture = target;
            captureCamera.enabled = false;
            var canvases = Object.FindObjectsByType<Canvas>().Where(canvas => canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
            var sorting = canvases.ToDictionary(canvas => canvas, canvas => (canvas.sortingLayerID, canvas.sortingOrder));
            var topLayer = SortingLayer.layers.Last().id;
            foreach (var canvas in canvases)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = captureCamera;
                canvas.planeDistance = 1f;
                canvas.sortingLayerID = topLayer;
                canvas.sortingOrder = OverlayOrder + sorting[canvas].sortingOrder;
            }

            yield return null;
            Canvas.ForceUpdateCanvases();

            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                RenderQuietly(captureCamera);
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                texture.Apply();
                var path = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, capturePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                TestContext.WriteLine($"captured {path}");
            }
            finally
            {
                RenderTexture.active = previous;
                foreach (var canvas in canvases.Where(canvas => canvas))
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.worldCamera = null;
                    canvas.sortingLayerID = sorting[canvas].sortingLayerID;
                    canvas.sortingOrder = sorting[canvas].sortingOrder;
                }

                Object.Destroy(texture);
                Object.Destroy(captureCamera.gameObject);
                Object.Destroy(target);
            }
        }

        private static void RenderQuietly(Camera camera)
        {
            var ignoring = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                camera.Render();
            }
            finally
            {
                LogAssert.ignoreFailingMessages = ignoring;
            }
        }
    }
}
