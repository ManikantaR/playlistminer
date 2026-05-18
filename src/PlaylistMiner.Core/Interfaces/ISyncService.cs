using PlaylistMiner.Core.DTOs;

namespace PlaylistMiner.Core.Interfaces;

public interface ISyncService
{
    Task<SyncResult> FullSyncAsync(CancellationToken ct = default);
    Task<SyncResult> SyncInboxAsync(CancellationToken ct = default);
}
