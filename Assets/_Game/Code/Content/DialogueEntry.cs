using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Content
{
    [Serializable]
    public sealed class DialogueEntry
    {
        public const int NoBranch = -1;

        [SerializeField] private List<DialogueLineEntry> lines = new();
        [SerializeField] private List<DialogueBranchEntry> branches = new();

        public IReadOnlyList<DialogueLineEntry> Lines => lines;

        public IReadOnlyList<DialogueBranchEntry> Branches => branches;

        public static DialogueEntry From(DialogueScript script)
        {
            var entry = new DialogueEntry();
            entry.lines = entry.Store(script.Lines);
            return entry;
        }

        public DialogueScript ToScript(string id) => new(id, Restore(lines, NoBranch));

        private List<DialogueLineEntry> Store(IEnumerable<ScriptLine> source) =>
            source.Select(line => DialogueLineEntry.From(line, StoreBranch)).ToList();

        private int StoreBranch(IReadOnlyList<ScriptLine> branchLines)
        {
            if (branchLines.Count == 0)
            {
                return NoBranch;
            }

            var branch = new DialogueBranchEntry();
            branches.Add(branch);
            var index = branches.Count - 1;
            branch.SetLines(Store(branchLines));
            return index;
        }

        private IEnumerable<ScriptLine> Restore(IEnumerable<DialogueLineEntry> source, int owner) =>
            (source ?? Enumerable.Empty<DialogueLineEntry>())
                .Where(line => line != null)
                .Select(line => line.ToLine(branch => BranchLines(branch, owner)))
                .ToList();

        private IEnumerable<ScriptLine> BranchLines(int branch, int owner) =>
            branches != null && branch > owner && branch < branches.Count && branches[branch] != null
                ? Restore(branches[branch].Lines, branch)
                : Enumerable.Empty<ScriptLine>();
    }

    [Serializable]
    public sealed class DialogueBranchEntry
    {
        [SerializeField] private List<DialogueLineEntry> lines = new();

        public IReadOnlyList<DialogueLineEntry> Lines => lines;

        internal void SetLines(List<DialogueLineEntry> branchLines)
        {
            lines = branchLines;
        }
    }

    [Serializable]
    public sealed class DialogueLineEntry
    {
        [SerializeField] private string speaker;
        [SerializeField, TextArea] private string text;
        [SerializeField] private List<StoryConditionEntry> conditions = new();
        [SerializeField] private List<StoryEffectEntry> effects = new();
        [SerializeField] private List<DialogueChoiceEntry> choices = new();

        public IReadOnlyList<DialogueChoiceEntry> Choices => choices;

        public static DialogueLineEntry From(ScriptLine line, Func<IReadOnlyList<ScriptLine>, int> storeBranch) => new()
        {
            speaker = line.SpeakerId,
            text = line.Text,
            conditions = line.Conditions.Select(StoryConditionEntry.From).ToList(),
            effects = line.Effects.Select(StoryEffectEntry.From).ToList(),
            choices = line.Choices.Select(choice => DialogueChoiceEntry.From(choice, storeBranch)).ToList()
        };

        public ScriptLine ToLine(Func<int, IEnumerable<ScriptLine>> branchLines) => new(
            speaker,
            text,
            (conditions ?? new List<StoryConditionEntry>()).Select(condition => condition.ToCondition()),
            (effects ?? new List<StoryEffectEntry>()).Select(effect => effect.ToEffect()),
            (choices ?? new List<DialogueChoiceEntry>()).Where(choice => choice != null).Select(choice => choice.ToChoice(branchLines)));
    }

    [Serializable]
    public sealed class DialogueChoiceEntry
    {
        [SerializeField] private string text;
        [SerializeField] private List<StoryEffectEntry> effects = new();
        [SerializeField] private int branch = DialogueEntry.NoBranch;

        public int Branch => branch;

        public static DialogueChoiceEntry From(DialogueChoice choice, Func<IReadOnlyList<ScriptLine>, int> storeBranch) => new()
        {
            text = choice.Text,
            effects = choice.Effects.Select(StoryEffectEntry.From).ToList(),
            branch = storeBranch(choice.Lines)
        };

        public DialogueChoice ToChoice(Func<int, IEnumerable<ScriptLine>> branchLines) => new(
            text,
            (effects ?? new List<StoryEffectEntry>()).Select(effect => effect.ToEffect()),
            branch == DialogueEntry.NoBranch ? Enumerable.Empty<ScriptLine>() : branchLines(branch));
    }

    [Serializable]
    public sealed class StoryConditionEntry
    {
        [SerializeField] private string flag;
        [SerializeField] private string value;
        [SerializeField] private bool absent;

        public static StoryConditionEntry From(StoryCondition condition) => new()
        {
            flag = condition.Flag,
            value = condition.Value,
            absent = condition.Absent
        };

        public StoryCondition ToCondition() => new(flag, value, absent);
    }

    [Serializable]
    public sealed class StoryEffectEntry
    {
        [SerializeField] private string flag;
        [SerializeField] private string value;

        public static StoryEffectEntry From(StoryEffect effect) => new()
        {
            flag = effect.Flag,
            value = effect.Value
        };

        public StoryEffect ToEffect() => new(flag, value);
    }
}
