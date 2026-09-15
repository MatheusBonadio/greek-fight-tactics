using System;

namespace Game.UI
{
    public sealed class Typewriter
    {
        public const float DefaultCharactersPerSecond = 30f;
        public const float MinimumCharactersPerSecond = 1f;

        private float revealed;
        private float charactersPerSecond = DefaultCharactersPerSecond;

        public string Text { get; private set; } = string.Empty;

        public int VisibleCharacters => (int)Math.Min(Text.Length, Math.Floor(revealed));

        public string VisibleText => Text.Substring(0, VisibleCharacters);

        public bool IsComplete => VisibleCharacters >= Text.Length;

        public float CharactersPerSecond
        {
            get => charactersPerSecond;
            set => charactersPerSecond = Math.Max(MinimumCharactersPerSecond, value);
        }

        public void Begin(string text)
        {
            Text = text ?? string.Empty;
            revealed = 0f;
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsComplete)
            {
                revealed += Math.Max(0f, deltaSeconds) * charactersPerSecond;
            }
        }

        public void Skip()
        {
            revealed = Text.Length;
        }
    }
}
