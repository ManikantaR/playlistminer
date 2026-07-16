namespace PlaylistMiner.Core.Exceptions;

public sealed class ManualInterventionRequiredException : Exception
{
    public ManualInterventionRequiredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
