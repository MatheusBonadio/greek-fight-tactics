using Unity.Cinemachine;
using UnityEngine;

namespace Game.UI
{
    [RequireComponent(typeof(Camera))]
    public sealed class PixelSnappedCamera : MonoBehaviour
    {
        public const int DefaultPixelsPerUnit = 16;
        public const float LagHysteresisPixels = 0.25f;

        private const float HalfPixel = 0.5f;
        private const float StillThreshold = 1e-8f;

        [SerializeField] private int pixelsPerUnit = DefaultPixelsPerUnit;

        private Vector2 heldLag;
        private Vector3 lastOutput;

        public int PixelsPerUnit => pixelsPerUnit;

        public static Vector3 Snap(Vector3 position, int pixelsPerUnit) =>
            new(Mathf.Round(position.x * pixelsPerUnit) / pixelsPerUnit, Mathf.Round(position.y * pixelsPerUnit) / pixelsPerUnit, position.z);

        private void OnEnable()
        {
            CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
        }

        private void OnDisable()
        {
            CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        }

        public static Vector2 HoldLag(Vector2 held, Vector2 lag, int pixelsPerUnit) =>
            new(HoldAxis(held.x, lag.x, pixelsPerUnit), HoldAxis(held.y, lag.y, pixelsPerUnit));

        private static float HoldAxis(float held, float lag, int pixelsPerUnit)
        {
            var driftPixels = Mathf.Abs(lag - held) * pixelsPerUnit;
            return driftPixels > HalfPixel + LagHysteresisPixels ? Mathf.Round(lag * pixelsPerUnit) / pixelsPerUnit : held;
        }

        private void OnCameraUpdated(CinemachineBrain brain)
        {
            if (!brain.OutputCamera || brain.OutputCamera.transform != transform)
            {
                return;
            }

            var target = (brain.ActiveVirtualCamera as CinemachineVirtualCameraBase)?.Follow;
            var camera = transform.position;
            var moving = ((Vector2)(camera - lastOutput)).sqrMagnitude > StillThreshold;
            lastOutput = camera;
            if (!target || !moving)
            {
                transform.position = Snap(camera, pixelsPerUnit);
                if (target)
                {
                    heldLag = Snap(camera - target.position, pixelsPerUnit);
                }

                return;
            }

            heldLag = HoldLag(heldLag, (Vector2)(camera - target.position), pixelsPerUnit);
            var anchor = Snap(target.position, pixelsPerUnit);
            transform.position = new Vector3(anchor.x + heldLag.x, anchor.y + heldLag.y, camera.z);
        }
    }
}
