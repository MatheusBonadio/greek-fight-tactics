using System;
using System.Collections.Generic;
using Game.Battle;
using Game.Core;

namespace Game.UI
{
    public enum BattleMenuLayer
    {
        Actions,
        Fallacies,
        Premises
    }

    public readonly struct BattleMenuChoice
    {
        public const int NoPremise = -1;

        public readonly ActionId Action;
        public readonly string FallacyId;
        public readonly int PremiseIndex;

        public BattleMenuChoice(ActionId action, string fallacyId = null, int premiseIndex = NoPremise)
        {
            Action = action;
            FallacyId = fallacyId;
            PremiseIndex = premiseIndex;
        }
    }

    public sealed class BattleMenuController
    {
        private readonly List<string> refutableFallacyIds;
        private IReadOnlyList<string> submenuOptions = Array.Empty<string>();

        public BattleMenuController(ActionMenuModel actions, IEnumerable<string> refutableFallacyIds)
        {
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
            this.refutableFallacyIds = new List<string>(refutableFallacyIds ?? throw new ArgumentNullException(nameof(refutableFallacyIds)));
        }

        public ActionMenuModel Actions { get; }

        public BattleMenuLayer Layer { get; private set; } = BattleMenuLayer.Actions;

        public IReadOnlyList<string> SubmenuOptions => submenuOptions;

        public int SubmenuCursor { get; private set; }

        public RejectionReason SelectedReason => Actions.Selected.Reason;

        public void Navigate(int delta)
        {
            if (Layer == BattleMenuLayer.Actions)
            {
                Actions.MoveCursor(delta);
                return;
            }

            var count = submenuOptions.Count;
            if (count > 0)
            {
                SubmenuCursor = ((SubmenuCursor + delta) % count + count) % count;
            }
        }

        public void Point(int index)
        {
            if (Layer == BattleMenuLayer.Actions)
            {
                if (index >= 0 && index < Actions.Entries.Count)
                {
                    Actions.TrySelect(Actions.Entries[index].Action);
                }

                return;
            }

            if (index >= 0 && index < submenuOptions.Count)
            {
                SubmenuCursor = index;
            }
        }

        public bool TryConfirm(out BattleMenuChoice choice)
        {
            choice = default;
            switch (Layer)
            {
                case BattleMenuLayer.Fallacies:
                    return TryChooseFromSubmenu(new BattleMenuChoice(ActionId.Refute, OptionAtCursor()), out choice);
                case BattleMenuLayer.Premises:
                    return TryChooseFromSubmenu(new BattleMenuChoice(ActionId.Maieutics, premiseIndex: SubmenuCursor), out choice);
            }

            if (!Actions.TryConfirm(out var action))
            {
                return false;
            }

            switch (action)
            {
                case ActionId.Refute:
                    Open(BattleMenuLayer.Fallacies, Actions.FallacyOptions(refutableFallacyIds));
                    return false;
                case ActionId.Maieutics:
                    Open(BattleMenuLayer.Premises, Actions.PremiseOptions());
                    return false;
                default:
                    choice = new BattleMenuChoice(action);
                    return true;
            }
        }

        public bool Cancel()
        {
            if (Layer == BattleMenuLayer.Actions)
            {
                return false;
            }

            Close();
            return true;
        }

        public void Refresh()
        {
            Actions.Refresh();
            if (Layer != BattleMenuLayer.Actions && !Actions.Selected.IsAvailable)
            {
                Close();
            }
        }

        private bool TryChooseFromSubmenu(BattleMenuChoice candidate, out BattleMenuChoice choice)
        {
            var hasOption = SubmenuCursor < submenuOptions.Count;
            choice = hasOption ? candidate : default;
            Close();
            return hasOption;
        }

        private string OptionAtCursor() => SubmenuCursor < submenuOptions.Count ? submenuOptions[SubmenuCursor] : null;

        private void Open(BattleMenuLayer layer, IReadOnlyList<string> options)
        {
            if (options.Count == 0)
            {
                return;
            }

            Layer = layer;
            submenuOptions = options;
            SubmenuCursor = 0;
        }

        private void Close()
        {
            Layer = BattleMenuLayer.Actions;
            submenuOptions = Array.Empty<string>();
            SubmenuCursor = 0;
        }
    }
}
