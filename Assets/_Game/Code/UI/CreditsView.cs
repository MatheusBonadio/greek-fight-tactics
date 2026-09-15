using System;
using Game.Content;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    public sealed class CreditsView : MonoBehaviour, IMenuPanel
    {
        public const string TitleKey = "credits.title";
        public const string BodyKey = "credits.body";

        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text body;
        [SerializeField] private UiTextSO text;

        public event Action Closed;

        public int Page { get; private set; } = 1;

        public int PageCount => Math.Max(1, body.textInfo.pageCount);

        public TMP_Text Body => body;

        public void Configure(TMP_Text titleLabel, TMP_Text bodyLabel, UiTextSO uiText)
        {
            title = titleLabel;
            body = bodyLabel;
            text = uiText;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            title.text = text.Get(TitleKey);
            body.overflowMode = TextOverflowModes.Page;
            body.text = text.Get(BodyKey);
            ShowPage(1);
        }

        public void HandleNavigate(Vector2 direction)
        {
            if (Mathf.Approximately(direction.y, 0f))
            {
                return;
            }

            ShowPage(Mathf.Clamp(Page + (direction.y > 0f ? -1 : 1), 1, PageCount));
        }

        public void HandleConfirm()
        {
            if (Page < PageCount)
            {
                ShowPage(Page + 1);
                return;
            }

            HandleCancel();
        }

        public void HandleCancel()
        {
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        public void HandlePoint(int row)
        {
        }

        private void ShowPage(int page)
        {
            Page = page;
            body.pageToDisplay = page;
            body.ForceMeshUpdate();
        }
    }
}
