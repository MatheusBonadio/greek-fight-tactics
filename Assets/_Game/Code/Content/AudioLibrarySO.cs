using System.Collections.Generic;
using UnityEngine;

namespace Game.Content
{
    public sealed class AudioLibrarySO : ScriptableObject
    {
        public const float DefaultPitch = 1f;

        [SerializeField] private Dictionary<string, AudioClip> clips = new();
        [SerializeField] private Dictionary<string, float> voices = new();

        public int Count => clips.Count;

        public IEnumerable<string> Ids => clips.Keys;

        public bool Contains(string id) => id != null && clips.ContainsKey(id);

        public AudioClip Get(string id) => id != null && clips.TryGetValue(id, out var clip) ? clip : null;

        public float PitchFor(string speakerId) =>
            speakerId != null && voices.TryGetValue(speakerId, out var pitch) ? pitch : DefaultPitch;

        public void SetClips(IReadOnlyDictionary<string, AudioClip> newClips)
        {
            clips = new Dictionary<string, AudioClip>(newClips);
        }

        public void SetVoices(IReadOnlyDictionary<string, float> newVoices)
        {
            voices = new Dictionary<string, float>(newVoices);
        }
    }
}
