using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class BarView : MonoBehaviour
    {
        public const float CriticalBlinkSeconds = 0.25f;
        public const int DefaultPixelWidth = 64;

        [SerializeField] private Image fill;
        [SerializeField] private Image flash;
        [SerializeField] private int pixelWidth = DefaultPixelWidth;
        [SerializeField] private Color normalTint = Color.white;
        [SerializeField] private Color criticalTint = new Color32(248, 120, 88, 255);

        private float blinkClock;

        public DrainingBar Bar { get; private set; }

        public bool CriticalTintShown { get; private set; }

        public void Configure(Image fillImage, Image flashImage, int widthInPixels)
        {
            fill = fillImage;
            flash = flashImage;
            pixelWidth = widthInPixels;
        }

        public void Bind(DrainingBar bar)
        {
            Bar = bar;
            blinkClock = 0f;
            Render();
        }

        public void Advance(float deltaSeconds)
        {
            if (Bar == null)
            {
                return;
            }

            Bar.Tick(deltaSeconds);
            blinkClock += Mathf.Max(0f, deltaSeconds);
            Render();
        }

        private void Update()
        {
            Advance(Time.deltaTime);
        }

        private void Render()
        {
            if (Bar == null)
            {
                return;
            }

            fill.fillAmount = UiPixels.Snap(Bar.DisplayedRatio, pixelWidth);
            flash.enabled = Bar.ConsumeFlashFrame();
            CriticalTintShown = Bar.IsCritical && Mathf.FloorToInt(blinkClock / CriticalBlinkSeconds) % 2 == 0;
            fill.color = CriticalTintShown ? criticalTint : normalTint;
        }
    }
}
