using Game.Core;

namespace Game.Net
{
    public enum ArgumentFetchStatus
    {
        Delivered,
        Declined,
        Invalid,
        Unavailable,
        Cancelled
    }

    public readonly struct ArgumentFetch
    {
        public readonly ArgumentFetchStatus Status;
        public readonly Argument Argument;
        public readonly string Json;
        public readonly int Attempts;
        public readonly string Error;

        private ArgumentFetch(ArgumentFetchStatus status, Argument argument, string json, int attempts, string error)
        {
            Status = status;
            Argument = argument;
            Json = json;
            Attempts = attempts;
            Error = error;
        }

        public bool IsDelivered => Status == ArgumentFetchStatus.Delivered;

        public static ArgumentFetch Delivered(Argument argument, string json, int attempts) => new(ArgumentFetchStatus.Delivered, argument, json, attempts, null);

        public static ArgumentFetch Declined(int attempts) => new(ArgumentFetchStatus.Declined, default, null, attempts, null);

        public static ArgumentFetch Invalid(int attempts, string error) => new(ArgumentFetchStatus.Invalid, default, null, attempts, error);

        public static ArgumentFetch Unavailable(int attempts, string error) => new(ArgumentFetchStatus.Unavailable, default, null, attempts, error);

        public static ArgumentFetch Cancelled() => new(ArgumentFetchStatus.Cancelled, default, null, 0, null);
    }
}
