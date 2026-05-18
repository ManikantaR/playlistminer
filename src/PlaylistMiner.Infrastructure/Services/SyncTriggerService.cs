using Microsoft.EntityFrameworkCore;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.Infrastructure.Services;

public class SyncTriggerService(PlaylistMinerDbContext db) : ISyncTrigger
{
    public async Task TriggerAsync(string syncType, CancellationToken ct = default)
    {
        var request = new SyncRequest
        {
            Type = syncType,
            Status = "pending",
            RequestedAt = DateTime.UtcNow
        };
        db.SyncRequests.Add(request);
        await db.SaveChangesAsync(ct);
    }

    public Task<SyncRequest?> GetPendingRequestAsync(CancellationToken ct = default)
        => db.SyncRequests
            .Where(r => r.Status == "pending")
            .OrderBy(r => r.RequestedAt)
            .FirstOrDefaultAsync(ct);

    public async Task MarkProcessingAsync(int requestId, CancellationToken ct = default)
    {
        var request = await db.SyncRequests.FindAsync([requestId], ct);
        if (request is null) return;
        request.Status = "processing";
        request.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkCompletedAsync(int requestId, CancellationToken ct = default)
    {
        var request = await db.SyncRequests.FindAsync([requestId], ct);
        if (request is null) return;
        request.Status = "completed";
        request.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(int requestId, string error, CancellationToken ct = default)
    {
        var request = await db.SyncRequests.FindAsync([requestId], ct);
        if (request is null) return;
        request.Status = "failed";
        request.Error = error;
        request.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public Task<SyncRequest?> GetLatestAsync(CancellationToken ct = default)
        => db.SyncRequests
            .OrderByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync(ct);
}
