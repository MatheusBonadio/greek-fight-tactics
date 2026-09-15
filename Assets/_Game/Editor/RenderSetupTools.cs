using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.EditorTools
{
    public static class RenderSetupTools
    {
        public const string CameraRigPrefabPath = "Assets/_Game/Prefabs/Camera/PixelPerfectCameraRig.prefab";
        public const string Renderer2DPath = "Assets/Settings/Renderer2D.asset";
        public const int ReferenceResolution = 224;
        public const int AssetsPixelsPerUnit = PixelArtImporter.PixelsPerUnit;
        public const float CameraDepth = -10f;
        public static readonly Vector3 TransparencySortAxis = new(0f, 1f, 0f);

        private const string CameraRigName = "PixelPerfectCameraRig";
        private const string SortModeProperty = "m_TransparencySortMode";
        private const string SortAxisProperty = "m_TransparencySortAxis";

        public static void Apply() => BatchRunner.Run(ApplyAll);

        public static void ApplyAll()
        {
            CreateCameraRigPrefab();
            ConfigureRendererSorting();
            AssetDatabase.SaveAssets();
        }

        public static void CreateCameraRigPrefab()
        {
            AssetFolders.Ensure(System.IO.Path.GetDirectoryName(CameraRigPrefabPath)?.Replace('\\', '/'));

            var root = new GameObject(CameraRigName);
            try
            {
                root.transform.position = new Vector3(0f, 0f, CameraDepth);

                var camera = root.AddComponent<Camera>();
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(16, 16, 32, 255);

                var pixelPerfect = root.AddComponent<PixelPerfectCamera>();
                pixelPerfect.assetsPPU = AssetsPixelsPerUnit;
                pixelPerfect.refResolutionX = ReferenceResolution;
                pixelPerfect.refResolutionY = ReferenceResolution;
                pixelPerfect.upscaleRT = true;
                pixelPerfect.cropFrameX = true;
                pixelPerfect.cropFrameY = true;

                PrefabUtility.SaveAsPrefabAsset(root, CameraRigPrefabPath, out var saved);
                if (!saved)
                {
                    throw new InvalidOperationException($"could not save {CameraRigPrefabPath}");
                }

                Debug.Log($"[RenderSetupTools] saved {CameraRigPrefabPath}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static void ConfigureRendererSorting()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableObject>(Renderer2DPath);
            if (renderer == null)
            {
                throw new InvalidOperationException($"missing {Renderer2DPath}");
            }

            var serialized = new SerializedObject(renderer);
            var sortMode = serialized.FindProperty(SortModeProperty) ?? throw new InvalidOperationException($"{Renderer2DPath} has no {SortModeProperty}");
            var sortAxis = serialized.FindProperty(SortAxisProperty) ?? throw new InvalidOperationException($"{Renderer2DPath} has no {SortAxisProperty}");

            sortMode.intValue = (int)TransparencySortMode.CustomAxis;
            sortAxis.vector3Value = TransparencySortAxis;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
            Debug.Log($"[RenderSetupTools] {Renderer2DPath}: transparency sort = CustomAxis {TransparencySortAxis}");
        }
    }
}
