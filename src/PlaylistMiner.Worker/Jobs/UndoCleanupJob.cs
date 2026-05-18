using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlaylistMiner.Infrastructure.Data;
using Quartz;

namespace PlaylistMiner.Worker.Jobs;

[DisallowConcurrentExecution]
public class UndoCleanupJob(PlaylistMinerDbContext db, ILogger<UndoCleanupJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var expired = await db.UndoLogs
            .Where(u => u.ExpiresAt < DateTime.UtcNow)
            .ToListAsync(context.CancellationToken);

        db.UndoLogs.RemoveRange(expired);
        await db.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation("Cleaned up {Count} expired undo entries", expired.Count);
    }
}
