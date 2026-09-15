using Game.Content;
using UnityEngine;

namespace Game.UI
{
    public sealed class FallacyRevealView : MonoBehaviour
    {
        public const float ActorTopUnits = 1.875f;
        public const float GapUnits = 0.125f;
        public const float IconHalfUnits = 0.5f;
        public const float IconOffsetUnits = ActorTopUnits + GapUnits + IconHalfUnits;
        public const int SortingOrderAboveActor = 100;

        [SerializeField] private SpriteRenderer iconRenderer;

        public SpriteRenderer IconRenderer => iconRenderer;

        public string RevealedFallacyId { get; private set; }

        public bool IsRevealed => iconRenderer && iconRenderer.enabled;

        public static FallacyRevealView Attach(ActorView actor)
        {
            var iconObject = new GameObject("FallacyIcon");
            iconObject.transform.SetParent(actor.transform, false);
            iconObject.transform.localPosition = new Vector3(0f, IconOffsetUnits, 0f);

            var renderer = iconObject.AddComponent<SpriteRenderer>();
            if (actor.SpriteRenderer)
            {
                renderer.sortingLayerID = actor.SpriteRenderer.sortingLayerID;
                renderer.sortingOrder = actor.SpriteRenderer.sortingOrder + SortingOrderAboveActor;
            }

            var view = iconObject.AddComponent<FallacyRevealView>();
            view.Configure(renderer);
            return view;
        }

        public void Configure(SpriteRenderer renderer)
        {
            iconRenderer = renderer;
            Hide();
        }

        public void Reveal(FallacySO fallacy)
        {
            if (!fallacy || !fallacy.Icon)
            {
                Hide();
                return;
            }

            iconRenderer.sprite = fallacy.Icon;
            iconRenderer.enabled = true;
            RevealedFallacyId = fallacy.Id;
        }

        public void Hide()
        {
            if (iconRenderer)
            {
                iconRenderer.enabled = false;
            }

            RevealedFallacyId = null;
        }
    }
}
