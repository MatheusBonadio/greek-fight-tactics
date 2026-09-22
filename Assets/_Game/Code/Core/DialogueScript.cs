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

        public IEnumerable<ScriptLine> AllLines() => Flatten(Lines);

        public IEnumerable<DialogueChoice> AllChoices() => AllLines().SelectMany(line => line.Choices);

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

            ValidateLines(Lines, Id, errors);
            return errors;
        }

        private static IEnumerable<ScriptLine> Flatten(IEnumerable<ScriptLine> lines) =>
            lines.SelectMany(line => new[] { line }.Concat(line.Choices.SelectMany(choice => Flatten(choice.Lines))));

        private static void ValidateLines(IReadOnlyList<ScriptLine> lines, string path, List<string> errors)
        {
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                var where = $"{path} line {index}";
                if (string.IsNullOrWhiteSpace(line.Text))
                {
                    errors.Add($"{where}: text is empty");
                }

                errors.AddRange(line.Conditions.Where(condition => !condition.IsValid).Select(condition => $"{where}: invalid condition {condition}"));
                ValidateEffects(line.Effects, where, errors);
                if (line.HasChoices)
                {
                    ValidateChoices(line.Choices, where, errors);
                }
            }
        }

        private static void ValidateChoices(IReadOnlyList<DialogueChoice> choices, string where, List<string> errors)
        {
            if (choices.Count < DialogueChoice.MinOptions || choices.Count > DialogueChoice.MaxOptions)
            {
                errors.Add($"{where}: a choice needs {DialogueChoice.MinOptions} to {DialogueChoice.MaxOptions} options, has {choices.Count}");
            }

            for (var index = 0; index < choices.Count; index++)
            {
                var option = choices[index];
                var at = $"{where} option {index}";
                if (string.IsNullOrWhiteSpace(option.Text))
                {
                    errors.Add($"{at}: text is empty");
                }
                else if (option.Text.Length > DialogueChoice.MaxTextLength)
                {
                    errors.Add($"{at}: \"{option.Text}\" is longer than {DialogueChoice.MaxTextLength} characters");
                }

                ValidateEffects(option.Effects, at, errors);
                ValidateLines(option.Lines, at, errors);
            }
        }

        private static void ValidateEffects(IEnumerable<StoryEffect> effects, string where, List<string> errors)
        {
            foreach (var effect in effects)
            {
                if (!effect.TryApply(new SaveData(), out var error))
                {
                    errors.Add($"{where}: invalid effect {effect}: {error}");
                }
            }
        }
    }
}
