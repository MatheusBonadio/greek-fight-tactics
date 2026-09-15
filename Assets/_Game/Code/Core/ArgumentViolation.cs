namespace Game.Core
{
    public enum ArgumentViolation
    {
        None,
        Malformed,
        MissingSpeech,
        SpeechTooShort,
        SpeechTooLong,
        UnknownFallacy,
        MissingPremises,
        EmptyPremise,
        WeakPremiseOutOfRange,
        UnknownTone
    }
}
