using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public readonly struct DebaterBadge
    {
        public readonly string Name;
        public readonly Sprite Portrait;

        public DebaterBadge(string name, Sprite portrait)
        {
            Name = name;
            Portrait = portrait;
        }
    }

    public sealed class DebaterPanelView : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private Image portrait;
        [SerializeField] private BarView coherence;
        [SerializeField] private TMP_Text valueLabel;

        private int shownValue = -1;
        private int shownMax = -1;

        public TMP_Text NameLabel => nameLabel;

        public Image Portrait => portrait;

        public BarView Coherence => coherence;

        public TMP_Text ValueLabel => valueLabel;

        public static string ValueText(int value, int max) =>
            string.Format(CultureInfo.InvariantCulture, "{0}/{1}", value, max);

        public void Configure(TMP_Text name, Image portraitImage, BarView coherenceBar, TMP_Text value)
        {
            nameLabel = name;
            portrait = portraitImage;
            coherence = coherenceBar;
            valueLabel = value;
        }

        public void Show(DebaterBadge badge)
        {
            nameLabel.text = badge.Name ?? string.Empty;
            portrait.sprite = badge.Portrait;
            portrait.enabled = badge.Portrait != null;
            if (badge.Portrait != null)
            {
                portrait.rectTransform.sizeDelta = badge.Portrait.rect.size;
            }
        }

        public void RenderValue()
        {
            var bar = coherence ? coherence.Bar : null;
            if (bar == null)
            {
                return;
            }

            var value = Mathf.CeilToInt(bar.Displayed);
            if (value == shownValue && bar.Max == shownMax)
            {
                return;
            }

            shownValue = value;
            shownMax = bar.Max;
            valueLabel.text = ValueText(value, bar.Max);
        }

        private void LateUpdate()
        {
            RenderValue();
        }
    }
}
