namespace PlaylistMiner.Core.Exceptions;

public class QuotaExhaustedException : Exception
{
    public QuotaExhaustedException()
        : base("YouTube API quota exhausted for today.")
    {
    }

    public QuotaExhaustedException(string message)
        : base(message)
    {
    }

    public QuotaExhaustedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
