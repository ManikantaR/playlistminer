using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlaylistMiner.Core.Interfaces;

namespace PlaylistMiner.Worker;

public class SyncTriggerHostedService(
    ISyncTrigger trigger,
    ISyncService syncService,
    ILogger<SyncTriggerHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var request = await trigger.GetPendingRequestAsync(stoppingToken);
                if (request is not null)
                {
                    await trigger.MarkProcessingAsync(request.Id, stoppingToken);
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
