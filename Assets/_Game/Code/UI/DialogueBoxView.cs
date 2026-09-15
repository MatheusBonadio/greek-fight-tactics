using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public readonly struct DialogueLine
    {
        public readonly string SpeakerName;
        public readonly Sprite Portrait;
        public readonly string Text;

        public DialogueLine(string speakerName, Sprite portrait, string text)
        {
            SpeakerName = speakerName;
            Portrait = portrait;
            Text = text;
        }
    }

    public sealed class DialogueBoxView : MonoBehaviour
    {
        public const int BoxPixelWidth = 208;

        private const int LayoutVisibleCharacters = 99999;

        [SerializeField] private GameObject box;
        [SerializeField] private TMP_Text body;
        [SerializeField] private TMP_Text speakerLabel;
        [SerializeField] private Image portrait;
        [SerializeField] private GameObject portraitFrame;

        private readonly Typewriter typewriter = new();
        private int pageStart;

        public event Action Finished;

        public bool IsOpen => box && box.activeSelf;

        public int Page { get; private set; }

        public int PageCount => Math.Max(1, body.textInfo.pageCount);

        public bool IsPageComplete => typewriter.IsComplete;

        public float CharactersPerSecond
        {
            get => typewriter.CharactersPerSecond;
            set => typewriter.CharactersPerSecond = value;
        }

        public TMP_Text Body => body;

        public TMP_Text SpeakerLabel => speakerLabel;

        public Image Portrait => portrait;

        public GameObject PortraitFrame => portraitFrame;

        public void Configure(GameObject dialogueBox, TMP_Text bodyText, TMP_Text speaker, Image portraitImage, GameObject frame)
        {
            box = dialogueBox;
            body = bodyText;
            speakerLabel = speaker;
            portrait = portraitImage;
            portraitFrame = frame;
        }

        public void Show(DialogueLine line)
        {
            box.SetActive(true);
            speakerLabel.text = line.SpeakerName ?? string.Empty;
            portrait.sprite = line.Portrait;
            portraitFrame.SetActive(line.Portrait != null);
            body.overflowMode = TextOverflowModes.Page;
            body.text = line.Text ?? string.Empty;
            ShowPage(1);
        }

        public void Advance(float deltaSeconds)
        {
            if (!IsOpen)
            {
                return;
            }

            typewriter.Tick(deltaSeconds);
            body.maxVisibleCharacters = pageStart + typewriter.VisibleCharacters;
        }

        public void Confirm()
        {
            if (!IsOpen)
            {
                return;
            }

            if (!typewriter.IsComplete)
            {
                typewriter.Skip();
                body.maxVisibleCharacters = pageStart + typewriter.VisibleCharacters;
                return;
            }

            if (Page < PageCount)
            {
                ShowPage(Page + 1);
                return;
            }

            box.SetActive(false);
            Finished?.Invoke();
        }

        private void Update()
        {
            Advance(Time.unscaledDeltaTime);
        }

        private void ShowPage(int page)
        {
            Page = page;
            body.pageToDisplay = page;
            body.maxVisibleCharacters = LayoutVisibleCharacters;
            body.ForceMeshUpdate();

            var info = body.textInfo;
            var hasPage = info.pageCount >= page && page > 0;
            pageStart = hasPage ? info.pageInfo[page - 1].firstCharacterIndex : 0;
            var pageEnd = hasPage ? info.pageInfo[page - 1].lastCharacterIndex : info.characterCount - 1;

            var pageText = new StringBuilder();
            for (var index = pageStart; index <= pageEnd && index < info.characterCount; index++)
            {
                pageText.Append(info.characterInfo[index].character);
            }

            typewriter.Begin(pageText.ToString());
            body.maxVisibleCharacters = pageStart;
        }
    }
}
