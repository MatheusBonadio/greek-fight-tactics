using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public sealed class ScriptLine
    {
        public ScriptLine(string speakerId, string text, IEnumerable<StoryCondition> conditions = null, IEnumerable<StoryEffect> effects = null)
        {
            SpeakerId = speakerId ?? string.Empty;
            Text = text ?? string.Empty;
            Conditions = (conditions ?? Enumerable.Empty<StoryCondition>()).ToArray();
            Effects = (effects ?? Enumerable.Empty<StoryEffect>()).ToArray();
        }

        public string SpeakerId { get; }

        public string Text { get; }

        public IReadOnlyList<StoryCondition> Conditions { get; }

        public IReadOnlyList<StoryEffect> Effects { get; }

        public bool IsNarration => SpeakerId.Length == 0;

        public bool IsAvailable(SaveData save)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            foreach (var condition in Conditions)
            {
                if (!condition.IsMet(save))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
