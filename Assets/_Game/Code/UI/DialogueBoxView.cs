using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

    public sealed class DialogueBoxView : MonoBehaviour, IMenuPanel
    {
        public const int BoxPixelWidth = 208;
        public const int ChoicePanelWidth = 128;
        public const int ChoicePadding = 4;
        public const int ChoiceRowHeight = 12;

        private const int LayoutVisibleCharacters = 99999;

        [SerializeField] private GameObject box;
        [SerializeField] private TMP_Text body;
        [SerializeField] private TMP_Text speakerLabel;
        [SerializeField] private Image portrait;
        [SerializeField] private GameObject portraitFrame;
        [SerializeField] private RectTransform choicePanel;
        [SerializeField] private TMP_Text[] choiceRows = Array.Empty<TMP_Text>();
        [SerializeField] private RectTransform choiceCursor;

        private readonly Typewriter typewriter = new();
        private readonly List<string> options = new();
        private MenuListModel choices;
        private float blinkClock;
        private int pageStart;

        public event Action Finished;

        public event Action<int> ChoiceMade;

        public bool IsOpen => box && box.activeSelf;

        public int Page { get; private set; }

        public int PageCount => Math.Max(1, body.textInfo.pageCount);

        public bool IsPageComplete => typewriter.IsComplete;

        public bool HasChoices => options.Count > 0;

        public bool IsAwaitingChoice => IsOpen && HasChoices && Page >= PageCount && typewriter.IsComplete;

        public IReadOnlyList<string> Options => options;

        public int ChoiceCursor => choices?.Cursor ?? -1;

        public float CharactersPerSecond
        {
            get => typewriter.CharactersPerSecond;
            set => typewriter.CharactersPerSecond = value;
        }

        public TMP_Text Body => body;

        public TMP_Text SpeakerLabel => speakerLabel;

        public Image Portrait => portrait;

        public GameObject PortraitFrame => portraitFrame;

        public RectTransform ChoicePanel => choicePanel;

        public IReadOnlyList<TMP_Text> ChoiceRows => choiceRows;

        public RectTransform ChoiceCursorRect => choiceCursor;

        public DialogueLine Line { get; private set; }

        public void Configure(GameObject dialogueBox, TMP_Text bodyText, TMP_Text speaker, Image portraitImage, GameObject frame)
        {
            box = dialogueBox;
            body = bodyText;
            speakerLabel = speaker;
            portrait = portraitImage;
            portraitFrame = frame;
        }

        public void ConfigureChoices(RectTransform panel, TMP_Text[] rows, RectTransform cursor)
        {
            choicePanel = panel;
            choiceRows = rows ?? Array.Empty<TMP_Text>();
            choiceCursor = cursor;
        }

        public void Show(DialogueLine line, IReadOnlyList<string> choiceTexts = null)
        {
            Line = line;
            options.Clear();
            if (choiceTexts != null)
            {
                options.AddRange(choiceTexts.Select(option => option ?? string.Empty));
            }

            choices = HasChoices
                ? new MenuListModel(options.Select((option, index) => new MenuEntry(index.ToString(CultureInfo.InvariantCulture), option)))
                : null;
            blinkClock = 0f;

            box.SetActive(true);
            speakerLabel.text = line.SpeakerName ?? string.Empty;
            portrait.sprite = line.Portrait;
            portraitFrame.SetActive(line.Portrait != null);
            body.overflowMode = TextOverflowModes.Page;
            body.text = line.Text ?? string.Empty;
            ShowPage(1);
            RenderChoices();
        }

        public void Advance(float deltaSeconds)
        {
            if (!IsOpen)
            {
                return;
            }

            typewriter.Tick(deltaSeconds);
            body.maxVisibleCharacters = pageStart + typewriter.VisibleCharacters;
            blinkClock += Mathf.Max(0f, deltaSeconds);
            RenderChoices();
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
                RenderChoices();
                return;
            }

            if (Page < PageCount)
            {
                ShowPage(Page + 1);
                RenderChoices();
                return;
            }

            if (HasChoices)
            {
                var chosen = choices.Cursor;
                Close();
                ChoiceMade?.Invoke(chosen);
                return;
            }

            Close();
            Finished?.Invoke();
        }

        public void HandleNavigate(Vector2 direction)
        {
            if (!IsAwaitingChoice || Mathf.Approximately(direction.y, 0f))
            {
                return;
            }

            choices.Move(direction.y > 0f ? -1 : 1);
            blinkClock = 0f;
            RenderChoices();
        }

        public void HandleConfirm()
        {
            if (IsAwaitingChoice)
            {
                Confirm();
            }
        }

        public void HandleCancel()
        {
        }

        public void HandlePoint(int row)
        {
            if (IsAwaitingChoice && choices.Point(row))
            {
                blinkClock = 0f;
                RenderChoices();
            }
        }

        private void Update()
        {
            Advance(Time.unscaledDeltaTime);
        }

        private void Close()
        {
            box.SetActive(false);
            options.Clear();
            choices = null;
            RenderChoices();
        }

        private void RenderChoices()
        {
            if (!choicePanel)
            {
                return;
            }

            var showing = IsAwaitingChoice;
            choicePanel.gameObject.SetActive(showing);
            if (!showing)
            {
                return;
            }

            choicePanel.sizeDelta = new Vector2(choicePanel.sizeDelta.x, 2 * ChoicePadding + options.Count * ChoiceRowHeight);
            for (var index = 0; index < choiceRows.Length; index++)
            {
                var used = index < options.Count;
                choiceRows[index].gameObject.SetActive(used);
                if (used && choiceRows[index].text != options[index])
                {
                    choiceRows[index].text = options[index];
                }
            }

            UiCursor.Place(choiceCursor, choices.Cursor < choiceRows.Length ? choiceRows[choices.Cursor] : null, UiCursor.IsBlinkOn(blinkClock));
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
