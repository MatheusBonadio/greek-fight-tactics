using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Content
{
    [Serializable]
    public sealed class ArgumentEntry
    {
        [SerializeField, TextArea] private string speech;
        [SerializeField] private string fallacyId;
        [SerializeField] private List<string> premises = new();
        [SerializeField] private int weakPremise;
        [SerializeField] private string tone = ArgumentTones.Arrogant;

        public static ArgumentEntry From(in Argument argument) => new()
        {
            speech = argument.Speech,
            fallacyId = argument.FallacyId,
            premises = (argument.Premises ?? Array.Empty<string>()).ToList(),
            weakPremise = argument.WeakPremise,
            tone = argument.Tone
        };

        public Argument ToArgument() =>
            new(speech, string.IsNullOrEmpty(fallacyId) ? null : fallacyId, premises ?? new List<string>(), weakPremise, tone);
    }
}
