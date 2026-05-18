using System.Diagnostics;
using PlaylistMiner.Core.Interfaces;

namespace PlaylistMiner.Infrastructure.Services;

public class ProcessRunner : IProcessRunner
{
    public async Task<int> RunAsync(string executable, string arguments, string outputFile, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(executable, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start process: {executable}");

        await using var outputStream = File.Create(outputFile);
        await process.StandardOutput.BaseStream.CopyToAsync(outputStream, ct);
        await process.WaitForExitAsync(ct);

        return process.ExitCode;
    }
}
