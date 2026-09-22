using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class AudienceMeterView : MonoBehaviour
    {
        [SerializeField] private Image playerFill;
        [SerializeField] private RectTransform marker;
        [SerializeField] private int pixelWidth = BarView.DefaultPixelWidth;
        [SerializeField] private TMP_Text playerShareLabel;
        [SerializeField] private TMP_Text opponentShareLabel;

        private int shownPlayerShare = -1;

        public AudienceMeter Meter { get; private set; }

        public TMP_Text PlayerShareLabel => playerShareLabel;

        public TMP_Text OpponentShareLabel => opponentShareLabel;

        public static string ShareText(int share) => string.Format(CultureInfo.InvariantCulture, "{0}%", share);

        public void Configure(Image playerFillImage, RectTransform markerTransform, int widthInPixels)
        {
            playerFill = playerFillImage;
            marker = markerTransform;
            pixelWidth = widthInPixels;
        }

        public void ConfigureShares(TMP_Text playerShare, TMP_Text opponentShare)
        {
            playerShareLabel = playerShare;
            opponentShareLabel = opponentShare;
        }

        public void Bind(AudienceMeter meter)
        {
            Meter = meter;
            shownPlayerShare = -1;
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

            RenderShares();
        }

        private void RenderShares()
        {
            var share = Mathf.RoundToInt(Meter.DisplayedPlayerShare);
            if (share == shownPlayerShare)
            {
                return;
            }

            shownPlayerShare = share;
            if (playerShareLabel)
            {
                playerShareLabel.text = ShareText(share);
            }

            if (opponentShareLabel)
            {
                opponentShareLabel.text = ShareText(AudienceMeter.MaxShare - share);
            }
        }
    }
}
