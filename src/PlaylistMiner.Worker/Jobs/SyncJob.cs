using Microsoft.Extensions.Logging;
using PlaylistMiner.Core.Interfaces;
using Quartz;

namespace PlaylistMiner.Worker.Jobs;

[DisallowConcurrentExecution]
public class SyncJob(ISyncService syncService, ILogger<SyncJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            logger.LogInformation("Starting full sync job");
            var result = await syncService.FullSyncAsync(context.CancellationToken);
            logger.LogInformation("Sync completed: {VideosProcessed} processed", result.VideosProcessed);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Sync job cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sync job failed");
        }
    }
}
