using Unity.Cinemachine;
using UnityEngine;

namespace Game.UI
{
    public sealed class ExplorationCamera : MonoBehaviour
    {
        public const float DefaultGlideSeconds = 0.6f;
        public const string DuelFocusName = "DuelFocus";

        [SerializeField] private CinemachineCamera virtualCamera;
        [SerializeField] private float explorationOrthographicSize = 7f;
        [SerializeField] private Transform duelFocus;
        [SerializeField] private float glideSeconds = DefaultGlideSeconds;
        [SerializeField] private int pixelsPerUnit = PixelSnappedCamera.DefaultPixelsPerUnit;

        private Transform explorationTarget;
        private Vector3 glideFrom;
        private Vector3 duelCenter;
        private float glideElapsed;
        private bool returning;

        public CinemachineCamera VirtualCamera => virtualCamera;

        public float ExplorationOrthographicSize => explorationOrthographicSize;

        public Transform DuelFocus => duelFocus;

        public Transform FollowTarget => virtualCamera ? virtualCamera.Follow : null;

        public float LensOrthographicSize => virtualCamera ? virtualCamera.Lens.OrthographicSize : explorationOrthographicSize;

        public bool IsFramingDuel { get; private set; }

        public bool IsGliding { get; private set; }

        public Vector2 DuelCenter => duelCenter;

        public float GlideSeconds
        {
            get => glideSeconds;
            set => glideSeconds = Mathf.Max(0f, value);
        }

        public void Configure(CinemachineCamera camera, float explorationSize, Transform focus)
        {
            virtualCamera = camera;
            explorationOrthographicSize = explorationSize;
            duelFocus = focus;
            var lens = virtualCamera.Lens;
            lens.OrthographicSize = explorationSize;
            virtualCamera.Lens = lens;
        }

        public void Follow(Transform target)
        {
            explorationTarget = target;
            if (!IsFramingDuel)
            {
                virtualCamera.Follow = target;
            }
        }

        public void FrameDuel(Vector2 worldCenter)
        {
            var focus = EnsureFocus();
            if (!IsFramingDuel)
            {
                explorationTarget = virtualCamera.Follow;
                focus.position = explorationTarget ? Snap(explorationTarget.position) : Snap(worldCenter);
                virtualCamera.Follow = focus;
            }

            duelCenter = new Vector3(worldCenter.x, worldCenter.y, 0f);
            BeginGlide(false);
            IsFramingDuel = true;
        }

        public void FrameExploration()
        {
            if (!IsFramingDuel)
            {
                return;
            }

            BeginGlide(true);
        }

        private void BeginGlide(bool back)
        {
            glideFrom = duelFocus.position;
            glideElapsed = 0f;
            returning = back;
            IsGliding = true;
            Glide(0f);
        }

        private void Update()
        {
            if (IsGliding)
            {
                Glide(Time.deltaTime);
            }
        }

        private void Glide(float deltaSeconds)
        {
            glideElapsed += deltaSeconds;
            var progress = glideSeconds <= 0f ? 1f : Mathf.Clamp01(glideElapsed / glideSeconds);
            var destination = returning && explorationTarget ? explorationTarget.position : duelCenter;
            duelFocus.position = Snap(Vector3.Lerp(glideFrom, destination, Mathf.SmoothStep(0f, 1f, progress)));
            if (progress < 1f)
            {
                return;
            }

            IsGliding = false;
            if (!returning)
            {
                return;
            }

            IsFramingDuel = false;
            virtualCamera.Follow = explorationTarget;
        }

        private Transform EnsureFocus()
        {
            if (!duelFocus)
            {
                duelFocus = new GameObject(DuelFocusName).transform;
            }

            return duelFocus;
        }

        private Vector3 Snap(Vector3 position) => PixelSnappedCamera.Snap(new Vector3(position.x, position.y, 0f), pixelsPerUnit);
    }
}
