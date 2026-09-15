namespace Game.Net
{
    public enum HttpOutcome
    {
        Completed,
        NetworkError,
        TimedOut
    }

    public readonly struct HttpResponseData
    {
        public readonly HttpOutcome Outcome;
        public readonly long Status;
        public readonly string Body;
        public readonly string Error;

        private HttpResponseData(HttpOutcome outcome, long status, string body, string error)
        {
            Outcome = outcome;
            Status = status;
            Body = body;
            Error = error;
        }

        public bool IsSuccess => Outcome == HttpOutcome.Completed && Status >= 200 && Status < 300;

        public static HttpResponseData Completed(long status, string body) => new(HttpOutcome.Completed, status, body, null);

        public static HttpResponseData Failed(string error) => new(HttpOutcome.NetworkError, 0, null, error);

        public static HttpResponseData Timeout() => new(HttpOutcome.TimedOut, 0, null, "timeout");
    }
}
