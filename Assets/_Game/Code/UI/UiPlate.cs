using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class UiPlate : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image background;
        [SerializeField] private int padding;

        private string fitted;

        public TMP_Text Label => label;

        public Image Background => background;

        public int Padding => padding;

        public static float WidthFor(TMP_Text label, string text, int padding) =>
            string.IsNullOrEmpty(text) ? 0f : Mathf.Ceil(label.GetPreferredValues(text).x) + 2 * padding;

        public void Configure(TMP_Text plateLabel, Image plateBackground, int horizontalPadding)
        {
            label = plateLabel;
            background = plateBackground;
            padding = horizontalPadding;
        }

        public void Fit()
        {
            fitted = label.text;
            var width = WidthFor(label, fitted, padding);
            background.enabled = width > 0f;
            var rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        }

        private void LateUpdate()
        {
            if (!ReferenceEquals(label.text, fitted))
            {
                Fit();
            }
        }
    }
}
