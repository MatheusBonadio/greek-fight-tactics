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
        [SerializeField] private List<DialogueLineEntry> lines = new();

        public IReadOnlyList<DialogueLineEntry> Lines => lines;

        public static DialogueEntry From(DialogueScript script) => new()
        {
            lines = script.Lines.Select(DialogueLineEntry.From).ToList()
        };

        public DialogueScript ToScript(string id) => new(id, lines.Select(line => line.ToLine()));
    }

    [Serializable]
    public sealed class DialogueLineEntry
    {
        [SerializeField] private string speaker;
        [SerializeField, TextArea] private string text;
        [SerializeField] private List<StoryConditionEntry> conditions = new();
        [SerializeField] private List<StoryEffectEntry> effects = new();

        public static DialogueLineEntry From(ScriptLine line) => new()
        {
            speaker = line.SpeakerId,
            text = line.Text,
            conditions = line.Conditions.Select(StoryConditionEntry.From).ToList(),
            effects = line.Effects.Select(StoryEffectEntry.From).ToList()
        };

        public ScriptLine ToLine() => new(speaker, text, conditions.Select(condition => condition.ToCondition()), effects.Select(effect => effect.ToEffect()));
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
