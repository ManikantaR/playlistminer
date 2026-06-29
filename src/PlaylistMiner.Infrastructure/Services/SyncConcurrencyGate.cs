using System.Threading;

namespace PlaylistMiner.Infrastructure.Services;

/// <summary>
/// Process-wide single-flight gate for sync work. Registered as a singleton so that a
/// scheduled full sync and an inbox sync (or a manually-triggered sync) can never write the
/// same playlist/video tables concurrently — concurrent writers caused Postgres lock
/// contention that stalled the linking phase indefinitely. Callers use <c>WaitAsync(0)</c>
/// to skip rather than queue, so overlapping schedules don't pile up.
/// </summary>
public sealed class SyncConcurrencyGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public Task<bool> TryAcquireAsync(CancellationToken ct = default) => _semaphore.WaitAsync(0, ct);

    public void Release() => _semaphore.Release();
}
