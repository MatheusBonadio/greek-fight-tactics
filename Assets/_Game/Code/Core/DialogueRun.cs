using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public sealed class DialogueRun
    {
        private readonly List<ScriptLine> lines;
        private readonly SaveData save;
        private readonly List<StoryEffect> applied = new();
        private readonly List<DialogueChoice> chosen = new();
        private int next;

        public DialogueRun(DialogueScript script, SaveData save)
        {
            Script = script ?? throw new ArgumentNullException(nameof(script));
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            lines = Available(script.Lines);
        }

        public DialogueScript Script { get; }

        public IReadOnlyList<ScriptLine> Lines => lines;

        public ScriptLine Current { get; private set; }

        public bool IsEmpty => lines.Count == 0;

        public bool IsAwaitingChoice { get; private set; }

        public bool IsFinished => !IsAwaitingChoice && next >= lines.Count;

        public IReadOnlyList<StoryEffect> AppliedEffects => applied;

        public IReadOnlyList<DialogueChoice> Chosen => chosen;

        public bool TryAdvance(out ScriptLine line)
        {
            if (IsAwaitingChoice)
            {
                throw new InvalidOperationException($"{Script.Id}: choose one of the options before advancing");
            }

            if (next >= lines.Count)
            {
                line = null;
                Current = null;
                return false;
            }

            line = lines[next++];
            Current = line;
            Apply(line.Effects);
            IsAwaitingChoice = line.HasChoices;
            return true;
        }

        public DialogueChoice Choose(int index)
        {
            if (!IsAwaitingChoice)
            {
                throw new InvalidOperationException($"{Script.Id}: no choice is waiting for an answer");
            }

            if (index < 0 || index >= Current.Choices.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"{Script.Id}: the choice has {Current.Choices.Count} options");
            }

            var choice = Current.Choices[index];
            IsAwaitingChoice = false;
            chosen.Add(choice);
            Apply(choice.Effects);
            lines.InsertRange(next, Available(choice.Lines));
            return choice;
        }

        private List<ScriptLine> Available(IEnumerable<ScriptLine> candidates) => candidates.Where(line => line.IsAvailable(save)).ToList();

        private void Apply(IEnumerable<StoryEffect> effects)
        {
            foreach (var effect in effects)
            {
                if (effect.TryApply(save, out _))
                {
                    applied.Add(effect);
                }
            }
        }
    }
}
