using System;
using System.Collections.Generic;
using System.Linq;
using Game.Net;

namespace Game.UI
{
    public enum SaveSlotsMode
    {
        Save,
        Load
    }

    public enum SaveSlotsStep
    {
        Choosing,
        ConfirmingOverwrite
    }

    public sealed class SaveSlotsModel
    {
        public const int ConfirmYes = 0;
        public const int ConfirmNo = 1;

        private readonly List<SaveSlotSummary> slots;

        public SaveSlotsModel(SaveSlotsMode mode, IEnumerable<SaveSlotSummary> summaries)
        {
            Mode = mode;
            slots = new List<SaveSlotSummary>(summaries ?? throw new ArgumentNullException(nameof(summaries)));
            if (slots.Count == 0)
            {
                throw new ArgumentException("At least one save slot is required.", nameof(summaries));
            }

            var first = slots.FindIndex(slot => IsEnabled(slot.Index));
            Cursor = first < 0 ? 0 : first;
        }

        public SaveSlotsMode Mode { get; }

        public IReadOnlyList<SaveSlotSummary> Slots => slots;

        public SaveSlotsStep Step { get; private set; } = SaveSlotsStep.Choosing;

        public int Cursor { get; private set; }

        public int ConfirmCursor { get; private set; } = ConfirmNo;

        public bool HasAnyEnabled => slots.Any(slot => IsEnabled(slot.Index));

        public bool IsEnabled(int index) =>
            index >= 0 && index < slots.Count && (Mode == SaveSlotsMode.Save || slots[index].IsUsable);

        public void Move(int delta)
        {
            if (Step == SaveSlotsStep.ConfirmingOverwrite)
            {
                if (delta != 0)
                {
                    ConfirmCursor = ConfirmCursor == ConfirmYes ? ConfirmNo : ConfirmYes;
                }

                return;
            }

            var step = Math.Sign(delta);
            if (step == 0 || !HasAnyEnabled)
            {
                return;
            }

            var index = Cursor;
            do
            {
                index = ((index + step) % slots.Count + slots.Count) % slots.Count;
            }
            while (!IsEnabled(index));

            Cursor = index;
        }

        public void Point(int index)
        {
            if (Step == SaveSlotsStep.ConfirmingOverwrite)
            {
                if (index == ConfirmYes || index == ConfirmNo)
                {
                    ConfirmCursor = index;
                }

                return;
            }

            if (IsEnabled(index))
            {
                Cursor = index;
            }
        }

        public bool TryConfirm(out int slotIndex)
        {
            slotIndex = Cursor;
            if (Step == SaveSlotsStep.ConfirmingOverwrite)
            {
                Step = SaveSlotsStep.Choosing;
                return ConfirmCursor == ConfirmYes;
            }

            if (!IsEnabled(Cursor))
            {
                return false;
            }

            if (Mode == SaveSlotsMode.Save && slots[Cursor].Exists)
            {
                Step = SaveSlotsStep.ConfirmingOverwrite;
                ConfirmCursor = ConfirmNo;
                return false;
            }

            return true;
        }

        public bool Cancel()
        {
            if (Step != SaveSlotsStep.ConfirmingOverwrite)
            {
                return false;
            }

            Step = SaveSlotsStep.Choosing;
            return true;
        }
    }
}
