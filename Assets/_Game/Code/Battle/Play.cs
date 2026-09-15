using Game.Core;

namespace Game.Battle
{
    public readonly struct Play
    {
        public const int NoTarget = 0;
        public const int NoPremise = -1;

        public readonly int ActorId;
        public readonly ActionId Action;
        public readonly int TargetId;
        public readonly Coord Destination;
        public readonly string ChosenFallacyId;
        public readonly int ChosenPremise;
        public readonly string QuoteId;
        public readonly Argument? PresentedArgument;

        private Play(
            int actorId,
            ActionId action,
            int targetId = NoTarget,
            Coord destination = default,
            string chosenFallacyId = null,
            int chosenPremise = NoPremise,
            string quoteId = null,
            Argument? presentedArgument = null)
        {
            ActorId = actorId;
            Action = action;
            TargetId = targetId;
            Destination = destination;
            ChosenFallacyId = chosenFallacyId;
            ChosenPremise = chosenPremise;
            QuoteId = quoteId;
            PresentedArgument = presentedArgument;
        }

        public static Play Circulate(int actorId, Coord destination) =>
            new(actorId, ActionId.Circulate, destination: destination);

        public static Play Refute(int actorId, int targetId, string chosenFallacyId) =>
            new(actorId, ActionId.Refute, targetId, chosenFallacyId: chosenFallacyId);

        public static Play Maieutics(int actorId, int targetId, int chosenPremise) =>
            new(actorId, ActionId.Maieutics, targetId, chosenPremise: chosenPremise);

        public static Play Quote(int actorId, string quoteId) =>
            new(actorId, ActionId.Quote, quoteId: quoteId);

        public static Play Concede(int actorId) =>
            new(actorId, ActionId.Concede);

        public static Play Withdraw(int actorId) =>
            new(actorId, ActionId.Withdraw);

        public static Play Argue(int actorId, int targetId, Argument argument) =>
            new(actorId, ActionId.Argue, targetId, presentedArgument: argument);
    }
}
