using Game.Core;
using UnityEngine;

namespace Game.Content
{
    public sealed class ArgumentSO : ScriptableObject
    {
        [SerializeField, TextArea] private string speech;
        [SerializeField] private string fallacyId;
        [SerializeField] private string[] premises = System.Array.Empty<string>();
        [SerializeField] private int weakPremise;
        [SerializeField] private string tone = ArgumentTones.Arrogant;

        public Argument ToArgument() =>
            new(speech, string.IsNullOrEmpty(fallacyId) ? null : fallacyId, premises, weakPremise, tone);
    }
}
