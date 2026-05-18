using PlaylistMiner.Core.Models;

namespace PlaylistMiner.Core.Interfaces;

public interface ISyncTrigger
{
    Task TriggerAsync(string syncType, CancellationToken ct = default);
    Task<SyncRequest?> GetPendingRequestAsync(CancellationToken ct = default);
    Task MarkProcessingAsync(int requestId, CancellationToken ct = default);
    Task MarkCompletedAsync(int requestId, CancellationToken ct = default);
    Task MarkFailedAsync(int requestId, string error, CancellationToken ct = default);
    Task<SyncRequest?> GetLatestAsync(CancellationToken ct = default);
}
