namespace PlaylistMiner.Core.Interfaces;

public interface IProcessRunner
{
    Task<int> RunAsync(string executable, string arguments, string outputFile, CancellationToken ct = default);
}
