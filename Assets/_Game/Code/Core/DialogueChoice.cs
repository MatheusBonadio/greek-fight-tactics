using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public sealed class DialogueChoice
    {
        public const int MinOptions = 2;
        public const int MaxOptions = 4;
        public const int MaxTextLength = 24;

        public DialogueChoice(string text, IEnumerable<StoryEffect> effects = null, IEnumerable<ScriptLine> lines = null)
        {
            Text = text ?? string.Empty;
            Effects = (effects ?? Enumerable.Empty<StoryEffect>()).ToArray();
            Lines = (lines ?? Enumerable.Empty<ScriptLine>()).ToArray();
        }

        public string Text { get; }

        public IReadOnlyList<StoryEffect> Effects { get; }

        public IReadOnlyList<ScriptLine> Lines { get; }
    }
}
