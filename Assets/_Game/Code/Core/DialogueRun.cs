using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public sealed class DialogueRun
    {
        private readonly IReadOnlyList<ScriptLine> lines;
        private readonly SaveData save;
        private readonly List<StoryEffect> applied = new();
        private int next;

        public DialogueRun(DialogueScript script, SaveData save)
        {
            Script = script ?? throw new ArgumentNullException(nameof(script));
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            lines = script.Lines.Where(line => line.IsAvailable(save)).ToArray();
        }

        public DialogueScript Script { get; }

        public IReadOnlyList<ScriptLine> Lines => lines;

        public ScriptLine Current { get; private set; }

        public bool IsEmpty => lines.Count == 0;

        public bool IsFinished => next >= lines.Count;

        public IReadOnlyList<StoryEffect> AppliedEffects => applied;

        public bool TryAdvance(out ScriptLine line)
        {
            if (IsFinished)
            {
                line = null;
                Current = null;
                return false;
            }

            line = lines[next++];
            Current = line;
            foreach (var effect in line.Effects)
            {
                if (effect.TryApply(save, out _))
                {
                    applied.Add(effect);
                }
            }

            return true;
        }
    }
}
