using Unity.Cinemachine;
using UnityEngine;

namespace Game.UI
{
    [RequireComponent(typeof(Camera))]
    public sealed class PixelSnappedCamera : MonoBehaviour
    {
        public const int DefaultPixelsPerUnit = 16;
        public const float HysteresisPixels = 0.35f;

        [SerializeField] private int pixelsPerUnit = DefaultPixelsPerUnit;

        private Vector2 shown;
        private bool settled;

        public int PixelsPerUnit => pixelsPerUnit;

        public static Vector3 Snap(Vector3 position, int pixelsPerUnit) =>
            new(Mathf.Round(position.x * pixelsPerUnit) / pixelsPerUnit, Mathf.Round(position.y * pixelsPerUnit) / pixelsPerUnit, position.z);

        public static float Steady(float shownValue, float wanted, int pixelsPerUnit) =>
            Mathf.Abs(wanted - shownValue) * pixelsPerUnit > 0.5f + HysteresisPixels
                ? Mathf.Round(wanted * pixelsPerUnit) / pixelsPerUnit
                : shownValue;

        private void OnEnable()
        {
            CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
        }

        private void OnDisable()
        {
            CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        }

        private void OnCameraUpdated(CinemachineBrain brain)
        {
            if (!brain.OutputCamera || brain.OutputCamera.transform != transform)
            {
                return;
            }

            var wanted = transform.position;
            if (!settled)
            {
                settled = true;
                shown = Snap(wanted, pixelsPerUnit);
            }
            else
            {
                shown = new Vector2(Steady(shown.x, wanted.x, pixelsPerUnit), Steady(shown.y, wanted.y, pixelsPerUnit));
            }

            transform.position = new Vector3(shown.x, shown.y, wanted.z);
        }
    }
}
