using System.Globalization;
using Game.Content;
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
        [SerializeField] private TMP_Text moraleLabel;
        [SerializeField] private UiTextSO text;
        [SerializeField] private Color favourColor = Color.white;
        [SerializeField] private Color againstColor = Color.white;

        private int shownValue = -1;
        private int shownMax = -1;

        public TMP_Text NameLabel => nameLabel;

        public Image Portrait => portrait;

        public BarView Coherence => coherence;

        public TMP_Text ValueLabel => valueLabel;

        public TMP_Text MoraleLabel => moraleLabel;

        public Color FavourColor => favourColor;

        public Color AgainstColor => againstColor;

        public static string ValueText(int value, int max) =>
            string.Format(CultureInfo.InvariantCulture, "{0}/{1}", value, max);

        public static string ShiftText(int shift) =>
            shift > 0 ? "+" + shift.ToString(CultureInfo.InvariantCulture) : shift.ToString(CultureInfo.InvariantCulture);

        public string MoraleText(int shift) =>
            string.Format(CultureInfo.InvariantCulture, text ? text.Get(UiTextKeys.MoraleLabel) : "{0}", ShiftText(shift));

        public void Configure(TMP_Text name, Image portraitImage, BarView coherenceBar, TMP_Text value)
        {
            nameLabel = name;
            portrait = portraitImage;
            coherence = coherenceBar;
            valueLabel = value;
        }

        public void ConfigureMorale(TMP_Text label, UiTextSO uiText, Color favour, Color against)
        {
            moraleLabel = label;
            text = uiText;
            favourColor = favour;
            againstColor = against;
            ShowMorale(0);
        }

        public void ShowMorale(int shift)
        {
            if (!moraleLabel)
            {
                return;
            }

            moraleLabel.text = shift == 0 ? string.Empty : MoraleText(shift);
            moraleLabel.color = shift > 0 ? favourColor : againstColor;
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
