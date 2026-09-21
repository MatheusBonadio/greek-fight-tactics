using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Content
{
    public sealed class DialogueLibrarySO : ScriptableObject
    {
        [SerializeField] private Dictionary<string, DialogueSpeaker> speakers = new();
        [SerializeField] private Dictionary<string, DialogueEntry> dialogues = new();

        public int Count => dialogues.Count;

        public IEnumerable<string> Ids => dialogues.Keys;

        public IReadOnlyDictionary<string, DialogueSpeaker> Speakers => speakers;

        public bool Contains(string id) => id != null && dialogues.ContainsKey(id);

        public bool TryGet(string id, out DialogueScript script)
        {
            script = null;
            if (id == null || !dialogues.TryGetValue(id, out var entry) || entry == null)
            {
                return false;
            }

            script = entry.ToScript(id);
            return true;
        }

        public string SpeakerName(string speakerId)
        {
            if (string.IsNullOrEmpty(speakerId))
            {
                return string.Empty;
            }

            return speakers.TryGetValue(speakerId, out var speaker) && speaker != null && !string.IsNullOrEmpty(speaker.DisplayName)
                ? speaker.DisplayName
                : UiTextSO.MissingPrefix + speakerId;
        }

        public void SetEntries(IReadOnlyDictionary<string, DialogueSpeaker> newSpeakers, IEnumerable<DialogueScript> scripts)
        {
            speakers = new Dictionary<string, DialogueSpeaker>(newSpeakers);
            dialogues = scripts.ToDictionary(script => script.Id, DialogueEntry.From);
        }
    }
}
