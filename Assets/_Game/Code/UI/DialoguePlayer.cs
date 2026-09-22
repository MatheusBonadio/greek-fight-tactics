using System;
using System.Linq;
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    public sealed class DialoguePlayer : IDisposable
    {
        private readonly DialogueBoxView box;
        private readonly Func<string, string> speakerName;
        private readonly Func<string, Sprite> portraitFor;
        private Action<DialogueRun> finished;

        public DialoguePlayer(DialogueBoxView box, Func<string, string> speakerName, Func<string, Sprite> portraitFor)
        {
            this.box = box ? box : throw new ArgumentNullException(nameof(box));
            this.speakerName = speakerName ?? throw new ArgumentNullException(nameof(speakerName));
            this.portraitFor = portraitFor ?? throw new ArgumentNullException(nameof(portraitFor));
            box.ChoiceMade += OnChoiceMade;
        }

        public DialogueRun Run { get; private set; }

        public bool IsPlaying => Run != null;

        public bool IsAwaitingChoice => IsPlaying && box.IsAwaitingChoice;

        public DialogueBoxView Box => box;

        public static bool IsPointerPress(InputAction action) => action?.activeControl?.device is Mouse;

        public bool TryPlay(DialogueRun run, Action<DialogueRun> onFinished)
        {
            if (IsPlaying || run == null || run.IsEmpty || run.IsFinished)
            {
                return false;
            }

            Run = run;
            finished = onFinished;
            ShowNext();
            return true;
        }

        public void Confirm()
        {
            if (!IsPlaying)
            {
                return;
            }

            if (box.IsAwaitingChoice)
            {
                box.Confirm();
                return;
            }

            box.Confirm();
            if (IsPlaying && !box.IsOpen && !Run.IsAwaitingChoice)
            {
                ShowNext();
            }
        }

        public void Poll(InputAction confirm, InputAction navigate)
        {
            if (!IsPlaying)
            {
                return;
            }

            if (box.IsAwaitingChoice)
            {
                if (navigate != null && navigate.WasPerformedThisFrame())
                {
                    box.HandleNavigate(navigate.ReadValue<Vector2>());
                }

                if (confirm != null && confirm.WasPressedThisFrame() && !IsPointerPress(confirm))
                {
                    Confirm();
                }

                return;
            }

            if (confirm != null && confirm.WasPressedThisFrame())
            {
                Confirm();
            }
        }

        public void Dispose()
        {
            if (box)
            {
                box.ChoiceMade -= OnChoiceMade;
            }
        }

        private void OnChoiceMade(int index)
        {
            if (!IsPlaying || !Run.IsAwaitingChoice)
            {
                return;
            }

            Run.Choose(index);
            ShowNext();
        }

        private void ShowNext()
        {
            if (!Run.TryAdvance(out var line))
            {
                var done = Run;
                var callback = finished;
                Run = null;
                finished = null;
                callback?.Invoke(done);
                return;
            }

            box.Show(
                new DialogueLine(speakerName(line.SpeakerId), portraitFor(line.SpeakerId), line.Text),
                line.HasChoices ? line.Choices.Select(choice => choice.Text).ToList() : null);
        }
    }
}
