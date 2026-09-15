using Game.Battle;
using Game.Core;

namespace Game.UI
{
    public readonly struct ActionMenuEntry
    {
        public readonly ActionId Action;
        public readonly RejectionReason Reason;

        public ActionMenuEntry(ActionId action, RejectionReason reason)
        {
            Action = action;
            Reason = reason;
        }

        public bool IsAvailable => Reason == RejectionReason.None;
    }
}
