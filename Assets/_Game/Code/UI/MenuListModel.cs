using System;
using System.Collections.Generic;

namespace Game.UI
{
    public readonly struct MenuEntry
    {
        public readonly string Id;
        public readonly string LabelKey;
        public readonly bool Enabled;

        public MenuEntry(string id, string labelKey, bool enabled = true)
        {
            Id = id;
            LabelKey = labelKey;
            Enabled = enabled;
        }
    }

    public sealed class MenuListModel
    {
        private readonly List<MenuEntry> entries;

        public MenuListModel(IEnumerable<MenuEntry> menuEntries)
        {
            entries = new List<MenuEntry>(menuEntries ?? throw new ArgumentNullException(nameof(menuEntries)));
            if (entries.Count == 0)
            {
                throw new ArgumentException("A menu needs at least one entry.", nameof(menuEntries));
            }

            Cursor = FirstEnabledIndex();
        }

        public IReadOnlyList<MenuEntry> Entries => entries;

        public int Cursor { get; private set; }

        public MenuEntry Selected => entries[Cursor];

        public void SetEnabled(string id, bool enabled)
        {
            var index = entries.FindIndex(entry => entry.Id == id);
            if (index < 0)
            {
                return;
            }

            entries[index] = new MenuEntry(entries[index].Id, entries[index].LabelKey, enabled);
            if (!Selected.Enabled)
            {
                Cursor = FirstEnabledIndex();
            }
        }

        public void Move(int delta)
        {
            var step = Math.Sign(delta);
            for (var moved = 0; moved < Math.Abs(delta); moved++)
            {
                var index = Cursor;
                for (var attempt = 0; attempt < entries.Count; attempt++)
                {
                    index = ((index + step) % entries.Count + entries.Count) % entries.Count;
                    if (entries[index].Enabled)
                    {
                        Cursor = index;
                        break;
                    }
                }
            }
        }

        public bool Point(int index)
        {
            if (index < 0 || index >= entries.Count || !entries[index].Enabled)
            {
                return false;
            }

            Cursor = index;
            return true;
        }

        public bool TryConfirm(out string id)
        {
            id = Selected.Id;
            return Selected.Enabled;
        }

        private int FirstEnabledIndex()
        {
            var index = entries.FindIndex(entry => entry.Enabled);
            return index < 0 ? 0 : index;
        }
    }
}
