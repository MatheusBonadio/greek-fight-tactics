using Game.Content;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    public sealed class UiTextLabel : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private UiTextSO text;
        [SerializeField] private string key;

        public string Key => key;

        public void Configure(TMP_Text target, UiTextSO uiText, string textKey)
        {
            label = target;
            text = uiText;
            key = textKey;
            Apply();
        }

        public void Apply()
        {
            if (label && text)
            {
                label.text = text.Get(key);
            }
        }

        private void Awake()
        {
            Apply();
        }
    }
}
