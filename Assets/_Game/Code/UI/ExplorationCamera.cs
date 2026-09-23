using Unity.Cinemachine;
using UnityEngine;

namespace Game.UI
{
    public sealed class ExplorationCamera : MonoBehaviour
    {
        public const float DeadZoneFraction = 2f / 3f;
        public const float DefaultAspect = 16f / 9f;
        public const float PinnedDeadZone = 1f;
        public const float MinimumDeadZone = 1f / 4f;
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

        private void Start()
        {
            TuneDeadZone();
        }

        public void TuneDeadZone()
        {
            if (!virtualCamera || !virtualCamera.TryGetComponent<CinemachinePositionComposer>(out var composer))
            {
                return;
            }

            var confiner = virtualCamera.GetComponent<CinemachineConfiner2D>();
            var bounds = confiner && confiner.BoundingShape2D ? confiner.BoundingShape2D.bounds.size : Vector3.zero;
            var brain = Brain;
            var view = new Vector2(LensOrthographicSize * (brain && brain.OutputCamera ? brain.OutputCamera.aspect : DefaultAspect),
                LensOrthographicSize) * 2f;
            var composition = composer.Composition;
            composition.DeadZone.Enabled = true;
            composition.DeadZone.Size = new Vector2(AxisDeadZone(bounds.x, view.x), AxisDeadZone(bounds.y, view.y));
            composer.Composition = composition;
        }

        private static float AxisDeadZone(float span, float view) =>
            view <= 0f || span <= view ? PinnedDeadZone : Mathf.Clamp((span - view) / view, MinimumDeadZone, DeadZoneFraction);

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
