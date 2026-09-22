using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    [RequireComponent(typeof(Image))]
    public sealed class DriftingSprite : MonoBehaviour
    {
        [SerializeField] private RectTransform frame;
        [SerializeField] private Vector2 frameSize = new(384f, 216f);
        [SerializeField] private Vector2 origin;
        [SerializeField] private float pixelsPerSecond = 2f;

        private Image image;
        private float drifted;

        public Vector2 Origin => origin;

        public float PixelsPerSecond => pixelsPerSecond;

        public Vector2 ArtPosition => new(Mathf.Repeat(origin.x + drifted + Size.x, frameSize.x + Size.x) - Size.x, origin.y);

        private Vector2 Size
        {
            get
            {
                image = image ? image : GetComponent<Image>();
                return image && image.sprite ? image.sprite.rect.size : Vector2.zero;
            }
        }

        public void Configure(RectTransform background, Vector2 painting, Vector2 start, float speed)
        {
            frame = background;
            frameSize = painting;
            origin = start;
            pixelsPerSecond = speed;
            drifted = 0f;
            Place();
        }

        public void Advance(float seconds)
        {
            drifted += pixelsPerSecond * seconds;
            Place();
        }

        private void Place()
        {
            if (!frame || frameSize.x <= 0f || frameSize.y <= 0f)
            {
                return;
            }

            var rect = (RectTransform)transform;
            var scale = frame.rect.width / frameSize.x;
            var art = ArtPosition;
            rect.sizeDelta = Size * scale;
            rect.anchoredPosition = new Vector2(Mathf.Round(art.x), Mathf.Round(art.y)) * scale;
        }

        private void Awake()
        {
            Place();
        }

        private void Update()
        {
            Advance(Time.unscaledDeltaTime);
        }
    }
}
