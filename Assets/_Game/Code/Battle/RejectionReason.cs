namespace Game.Battle
{
    public enum RejectionReason
    {
        None,
        BattleOver,
        UnknownActor,
        NotActorsTurn,
        ActionNotAllowed,
        UnknownTarget,
        TargetOutOfRange,
        DestinationUnreachable,
        NoArgumentToAnswer,
        ArgumentNotRefutable,
        InvalidPremise,
        NoQuoteUses,
        MissingArgument
    }
}
