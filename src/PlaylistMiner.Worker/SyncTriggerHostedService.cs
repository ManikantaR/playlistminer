using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlaylistMiner.Core.Interfaces;

namespace PlaylistMiner.Worker;

public class SyncTriggerHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<SyncTriggerHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workerInstance = Environment.MachineName;
        var hostEnv = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") 
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") 
            ?? "Production";

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();

                try
                {
                    var tracker = scope.ServiceProvider.GetRequiredService<IPipelineRunTracker>();
                    await tracker.RecordWorkerHeartbeatAsync(workerInstance, hostEnv, "idle", stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to record worker heartbeat");
                }

                var trigger = scope.ServiceProvider.GetRequiredService<ISyncTrigger>();
                var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();

                var request = await trigger.GetPendingRequestAsync(stoppingToken);
                if (request is not null)
                {
                    await trigger.MarkProcessingAsync(request.Id, stoppingToken);

                    try
                    {
                        var tracker = scope.ServiceProvider.GetRequiredService<IPipelineRunTracker>();
                        await tracker.RecordWorkerHeartbeatAsync(workerInstance, hostEnv, request.Type == "inbox" ? "sync_inbox" : "sync_full", stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to update active worker heartbeat");
                    }

                    try
                    {
                        if (request.Type == "inbox")
                            await syncService.SyncInboxAsync(stoppingToken);
                        else
                            await syncService.FullSyncAsync(stoppingToken);

                        await trigger.MarkCompletedAsync(request.Id, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        await trigger.MarkFailedAsync(request.Id, ex.Message, stoppingToken);
                        logger.LogError(ex, "Triggered sync failed for request {RequestId}", request.Id);
                    }
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SyncTriggerHostedService error");
                try { await Task.Delay(5000, stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }
    }
}
