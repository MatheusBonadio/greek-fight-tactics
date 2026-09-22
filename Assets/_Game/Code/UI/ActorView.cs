using Game.Core;
using Game.Grid;
using UnityEngine;

namespace Game.UI
{
    public sealed class ActorView : MonoBehaviour
    {
        public const string IdleState = "idle";
        public const string WalkState = "walk";
        public const string ActionState = "action";
        public const string DirectionXParameter = "dirX";
        public const string DirectionYParameter = "dirY";

        [SerializeField] private Vector2Int startCell;
        [SerializeField] private Facing startFacing = Facing.SouthEast;
        [SerializeField] private int startLevel;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Animator animator;

        private string playingState;
        private Posture posture = Posture.Idle;

        public Vector2Int StartCell => startCell;

        public Facing StartFacing => startFacing;

        public int StartLevel => startLevel;

        public SpriteRenderer SpriteRenderer => spriteRenderer;

        public GridWalker Walker { get; private set; }

        public Posture Posture
        {
            get => posture;
            set
            {
                posture = value;
                Sync();
            }
        }

        public Color Tint
        {
            get => spriteRenderer ? spriteRenderer.color : Color.white;
            set
            {
                if (spriteRenderer)
                {
                    spriteRenderer.color = value;
                }
            }
        }

        public string PlayingState => playingState;

        public static string StateFor(Posture posture, bool stepping) => stepping ? WalkState : posture == Posture.Action ? ActionState : IdleState;

        public bool IsShown
        {
            get => spriteRenderer == null || spriteRenderer.enabled;
            set
            {
                if (spriteRenderer != null)
                {
                    spriteRenderer.enabled = value;
                }
            }
        }

        public void Configure(Vector2Int cell, Facing facing, SpriteRenderer renderer, Animator actorAnimator, int level = TileData.GroundLevel)
        {
            startCell = cell;
            startFacing = facing;
            startLevel = level;
            spriteRenderer = renderer;
            animator = actorAnimator;
            var coord = new Coord(cell.x, cell.y);
            var ground = IsoMath.GridToWorld(coord);
            ApplyPose(ground, level, TerrainSorting.ActorDepth(ground, level, IsoMath.Depth(coord)), facing, false);
        }

        public void Bind(GridWalker walker)
        {
            Walker = walker;
            Sync();
        }

        private void LateUpdate() => Sync();

        private void Sync()
        {
            if (Walker != null)
            {
                ApplyPose(Walker.WorldPosition, Walker.Level, Walker.SortDepth, Walker.Facing, Walker.IsStepping);
            }
        }

        private void ApplyPose(Vector2 ground, float level, float depth, Facing facing, bool stepping)
        {
            transform.position = new Vector3(ground.x, ground.y + IsoMath.Lift(level), depth);
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = FacingRules.IsMirrored(facing);
            }

            if (animator == null || animator.runtimeAnimatorController == null || !Application.isPlaying)
            {
                return;
            }

            animator.SetFloat(DirectionXParameter, 1f);
            animator.SetFloat(DirectionYParameter, FacingRules.SheetFacing(facing) == Facing.NorthEast ? 1f : -1f);

            var state = StateFor(posture, stepping);
            if (state == playingState)
            {
                return;
            }

            animator.Play(state);
            playingState = state;
        }
    }
}
