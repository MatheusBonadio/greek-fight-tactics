using Unity.Cinemachine;
using UnityEngine;

namespace Game.UI
{
    public sealed class ExplorationCamera : MonoBehaviour
    {
        public const float DefaultGlideSeconds = 0.6f;
        public const string DuelFocusName = "DuelFocus";
        public const string DuelCameraName = "DuelCamera";
        public const int DuelPriority = 1;

        [SerializeField] private CinemachineCamera virtualCamera;
        [SerializeField] private CinemachineCamera duelCamera;
        [SerializeField] private float explorationOrthographicSize = 7f;
        [SerializeField] private Transform duelFocus;
        [SerializeField] private int pixelsPerUnit = PixelSnappedCamera.DefaultPixelsPerUnit;

        private Vector2 duelCenter;

        public CinemachineCamera VirtualCamera => virtualCamera;

        public CinemachineCamera DuelCamera => duelCamera;

        public float ExplorationOrthographicSize => explorationOrthographicSize;

        public Transform DuelFocus => duelFocus;

        public Transform FollowTarget => IsFramingDuel ? duelCamera.Follow : virtualCamera ? virtualCamera.Follow : null;

        public float LensOrthographicSize => virtualCamera ? virtualCamera.Lens.OrthographicSize : explorationOrthographicSize;

        public bool IsFramingDuel => duelCamera && duelCamera.enabled;

        public bool IsGliding => Brain && Brain.IsBlending;

        public Vector2 DuelCenter => duelCenter;

        public static Vector2 FramingFor(Vector2 duelCenter, float orthographicSize) =>
            new(duelCenter.x, duelCenter.y - HudLayout.BottomStripFraction * orthographicSize);

        public void Configure(CinemachineCamera camera, CinemachineCamera duel, float explorationSize, Transform focus)
        {
            virtualCamera = camera;
            duelCamera = duel;
            explorationOrthographicSize = explorationSize;
            duelFocus = focus;
            SetOrthographicSize(camera, explorationSize);
            SetOrthographicSize(duel, explorationSize);
            duel.Follow = focus;
            duel.Priority = DuelPriority;
            duel.enabled = false;
        }

        public void Follow(Transform target)
        {
            virtualCamera.Follow = target;
        }

        public void FrameDuel(Vector2 worldCenter)
        {
            duelCenter = worldCenter;
            AimAtDuel();
            HoldOutputZoom(duelCamera);
            duelCamera.enabled = true;
        }

        public void FrameExploration()
        {
            if (!IsFramingDuel)
            {
                return;
            }

            HoldOutputZoom(virtualCamera);
            duelCamera.enabled = false;
        }

        private void OnEnable() => CinemachineCore.BlendFinishedEvent.AddListener(OnBlendFinished);

        private void OnDisable() => CinemachineCore.BlendFinishedEvent.RemoveListener(OnBlendFinished);

        private void OnBlendFinished(ICinemachineMixer mixer, ICinemachineCamera camera)
        {
            if (ReferenceEquals(camera, virtualCamera))
            {
                SetOrthographicSize(virtualCamera, explorationOrthographicSize);
            }
        }

        private void Update()
        {
            if (IsFramingDuel)
            {
                AimAtDuel();
            }
        }

        private void HoldOutputZoom(CinemachineCamera incoming) => SetOrthographicSize(incoming, OutputOrthographicSize);

        private static void SetOrthographicSize(CinemachineCamera camera, float orthographicSize)
        {
            var lens = camera.Lens;
            lens.OrthographicSize = orthographicSize;
            camera.Lens = lens;
        }

        private void AimAtDuel()
        {
            var framing = FramingFor(duelCenter, OutputOrthographicSize);
            duelFocus.position = PixelSnappedCamera.Snap(new Vector3(framing.x, framing.y, 0f), pixelsPerUnit);
        }

        private float OutputOrthographicSize
        {
            get
            {
                var output = Brain ? Brain.OutputCamera : null;
                return output && output.orthographic ? output.orthographicSize : LensOrthographicSize;
            }
        }

        private CinemachineBrain Brain => virtualCamera ? CinemachineCore.FindPotentialTargetBrain(virtualCamera) : null;
    }
}
