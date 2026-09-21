using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public sealed class DialogueScript
    {
        public DialogueScript(string id, IEnumerable<ScriptLine> lines)
        {
            Id = id ?? string.Empty;
            Lines = (lines ?? throw new ArgumentNullException(nameof(lines))).ToArray();
        }

        public string Id { get; }

        public IReadOnlyList<ScriptLine> Lines { get; }

        public DialogueRun Begin(SaveData save) => new(this, save);

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(Id))
            {
                errors.Add("dialogue has no id");
            }

            if (Lines.Count == 0)
            {
                errors.Add($"{Id}: dialogue has no lines");
            }

            for (var index = 0; index < Lines.Count; index++)
            {
                var line = Lines[index];
                if (string.IsNullOrWhiteSpace(line.Text))
                {
                    errors.Add($"{Id} line {index}: text is empty");
                }

                errors.AddRange(line.Conditions.Where(condition => !condition.IsValid).Select(condition => $"{Id} line {index}: invalid condition {condition}"));
                foreach (var effect in line.Effects)
                {
                    if (!effect.TryApply(new SaveData(), out var error))
                    {
                        errors.Add($"{Id} line {index}: invalid effect {effect}: {error}");
                    }
                }
            }

            return errors;
        }
    }
}
