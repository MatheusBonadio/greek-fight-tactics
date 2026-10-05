using System.Collections.Generic;
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
        [SerializeField] private SpriteRenderer shadowRenderer;

        public const float OneShotCapSeconds = 2.5f;
        public const float ShadowOpacity = 0.45f;
        public const int PixelsPerUnit = PixelSnappedCamera.DefaultPixelsPerUnit;

        private string playingState;
        private Posture posture = Posture.Idle;
        private string oneShot;
        private float oneShotRemaining;

        public Vector2Int StartCell => startCell;

        public Facing StartFacing => startFacing;

        public int StartLevel => startLevel;

        public SpriteRenderer SpriteRenderer => spriteRenderer;

        public SpriteRenderer ShadowRenderer => shadowRenderer;

        public IEnumerable<SpriteRenderer> Renderers
        {
            get
            {
                if (spriteRenderer != null)
                {
                    yield return spriteRenderer;
                }

                if (shadowRenderer != null)
                {
                    yield return shadowRenderer;
                }
            }
        }

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

        public bool IsPlayingOneShot => oneShotRemaining > 0f;

        public bool PlayOnce(string animation)
        {
            if (string.IsNullOrEmpty(animation) || !HasState(animation))
            {
                return false;
            }

            oneShot = animation;
            oneShotRemaining = BattleAnimations.Holds(animation) ? float.PositiveInfinity : OneShotCapSeconds;
            Sync();
            return true;
        }

        public void StopOneShot()
        {
            oneShot = null;
            oneShotRemaining = 0f;
            Sync();
        }

        public bool HasState(string animation) =>
            animator != null && animator.runtimeAnimatorController != null
            && animator.HasState(0, Animator.StringToHash(animation));

        public string StateFor(Posture stance, bool stepping)
        {
            if (stepping)
            {
                return WalkState;
            }

            if (stance != Posture.Action)
            {
                return IdleState;
            }

            return HasState(BattleAnimations.Talk) ? BattleAnimations.Talk : ActionState;
        }

        public bool IsShown
        {
            get => spriteRenderer == null || spriteRenderer.enabled;
            set
            {
                if (spriteRenderer != null)
                {
                    spriteRenderer.enabled = value;
                }

                if (shadowRenderer != null)
                {
                    shadowRenderer.enabled = value;
                }
            }
        }

        public void Configure(Vector2Int cell, Facing facing, SpriteRenderer renderer, Animator actorAnimator, int level = TileData.GroundLevel, SpriteRenderer shadow = null)
        {
            startCell = cell;
            startFacing = facing;
            startLevel = level;
            spriteRenderer = renderer;
            animator = actorAnimator;
            shadowRenderer = shadow;
            if (shadowRenderer != null)
            {
                shadowRenderer.color = new Color(1f, 1f, 1f, ShadowOpacity);
            }

            var coord = new Coord(cell.x, cell.y);
            var ground = IsoMath.GridToWorld(coord);
            ApplyPose(ground, level, TerrainSorting.ActorDepth(ground, level, IsoMath.Depth(coord)), facing, false);
        }

        public void Bind(GridWalker walker)
        {
            Walker = walker;
            Sync();
        }

        private void LateUpdate()
        {
            AdvanceOneShot(Time.deltaTime);
            Sync();
        }

        private void AdvanceOneShot(float seconds)
        {
            if (oneShotRemaining <= 0f || float.IsPositiveInfinity(oneShotRemaining))
            {
                return;
            }

            oneShotRemaining -= seconds;
            if (oneShotRemaining <= 0f || Finished(oneShot))
            {
                oneShot = null;
                oneShotRemaining = 0f;
            }
        }

        private bool Finished(string animation)
        {
            if (animator == null || !Application.isPlaying || playingState != animation)
            {
                return false;
            }

            var state = animator.GetCurrentAnimatorStateInfo(0);
            return state.shortNameHash == Animator.StringToHash(animation) && state.normalizedTime >= 1f;
        }

        private void Sync()
        {
            if (Walker != null)
            {
                ApplyPose(Walker.WorldPosition, Walker.Level, Walker.SortDepth, Walker.Facing, Walker.IsStepping);
            }
        }

        private void ApplyPose(Vector2 ground, float level, float depth, Facing facing, bool stepping)
        {
            var drawn = PixelSnappedCamera.Snap(new Vector3(ground.x, ground.y + IsoMath.Lift(level), 0f), PixelsPerUnit);
            transform.position = new Vector3(drawn.x, drawn.y, depth);
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = FacingRules.IsMirrored(facing);
            }

            if (shadowRenderer != null)
            {
                shadowRenderer.transform.localPosition = new Vector3(
                    -IsoMath.CellWidth,
                    -IsoMath.CellHeight,
                    TerrainSorting.TileSortPoint - TerrainSorting.ShadowOffset);
            }

            if (animator == null || animator.runtimeAnimatorController == null || !Application.isPlaying)
            {
                return;
            }

            animator.SetFloat(DirectionXParameter, 1f);
            animator.SetFloat(DirectionYParameter, FacingRules.SheetFacing(facing) == Facing.NorthEast ? 1f : -1f);

            var state = oneShotRemaining > 0f ? oneShot : StateFor(posture, stepping);
            if (state == playingState)
            {
                return;
            }

            animator.Play(state);
            playingState = state;
        }
    }
}
