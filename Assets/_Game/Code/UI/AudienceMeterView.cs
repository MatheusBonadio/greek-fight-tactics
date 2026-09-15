using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class AudienceMeterView : MonoBehaviour
    {
        [SerializeField] private Image playerFill;
        [SerializeField] private RectTransform marker;
        [SerializeField] private int pixelWidth = BarView.DefaultPixelWidth;

        public AudienceMeter Meter { get; private set; }

        public void Configure(Image playerFillImage, RectTransform markerTransform, int widthInPixels)
        {
            playerFill = playerFillImage;
            marker = markerTransform;
            pixelWidth = widthInPixels;
        }

        public void Bind(AudienceMeter meter)
        {
            Meter = meter;
            Render();
        }

        public void Advance(float deltaSeconds)
        {
            if (Meter == null)
            {
                return;
            }

            Meter.Tick(deltaSeconds);
            Render();
        }

        private void Update()
        {
            Advance(Time.deltaTime);
        }

        private void Render()
        {
            if (Meter == null)
            {
                return;
            }

            var ratio = UiPixels.Snap(Meter.DisplayedPlayerShare / AudienceMeter.MaxShare, pixelWidth);
            playerFill.fillAmount = ratio;
            if (marker)
            {
                marker.anchoredPosition = new Vector2(Mathf.Round(ratio * pixelWidth) - pixelWidth / 2f, marker.anchoredPosition.y);
            }
        }
    }
}
