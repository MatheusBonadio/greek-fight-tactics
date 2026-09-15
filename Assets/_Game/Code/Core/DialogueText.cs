using System;
using System.Collections.Generic;
using System.Text;

namespace Game.Core
{
    public static class DialogueText
    {
        public const int DefaultCharactersPerLine = 34;
        public const int DefaultLinesPerPage = 4;

        public static IReadOnlyList<string> Wrap(string text, int charactersPerLine = DefaultCharactersPerLine)
        {
            if (charactersPerLine <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(charactersPerLine), charactersPerLine, "Line width must be positive.");
            }

            var lines = new List<string>();
            var current = new StringBuilder();
            foreach (var word in (text ?? string.Empty).Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var remaining = word;
                while (remaining.Length > charactersPerLine)
                {
                    Flush(current, lines);
                    lines.Add(remaining.Substring(0, charactersPerLine));
                    remaining = remaining.Substring(charactersPerLine);
                }

                var needed = current.Length == 0 ? remaining.Length : current.Length + 1 + remaining.Length;
                if (needed > charactersPerLine)
                {
                    Flush(current, lines);
                }

                if (current.Length > 0)
                {
                    current.Append(' ');
                }

                current.Append(remaining);
            }

            Flush(current, lines);
            return lines;
        }

        public static IReadOnlyList<IReadOnlyList<string>> Paginate(string text, int charactersPerLine = DefaultCharactersPerLine, int linesPerPage = DefaultLinesPerPage)
        {
            if (linesPerPage <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(linesPerPage), linesPerPage, "Page height must be positive.");
            }

            var lines = Wrap(text, charactersPerLine);
            var pages = new List<IReadOnlyList<string>>();
            for (var start = 0; start < lines.Count; start += linesPerPage)
            {
                var count = Math.Min(linesPerPage, lines.Count - start);
                var page = new List<string>(count);
                for (var offset = 0; offset < count; offset++)
                {
                    page.Add(lines[start + offset]);
                }

                pages.Add(page);
            }

            return pages;
        }

        private static void Flush(StringBuilder current, List<string> lines)
        {
            if (current.Length == 0)
            {
                return;
            }

            lines.Add(current.ToString());
            current.Clear();
        }
    }
}
