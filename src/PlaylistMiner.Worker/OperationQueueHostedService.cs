using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlaylistMiner.Core.Interfaces;

namespace PlaylistMiner.Worker;

public class OperationQueueHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<OperationQueueHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var queue = scope.ServiceProvider.GetRequiredService<IOperationQueueService>();
                var operation = await queue.GetNextRunnableAsync(DateTime.UtcNow, stoppingToken);

                if (operation is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                    continue;
                }

                try
                {
                    logger.LogInformation(
                        "Executing operation request {OperationId} of type {OperationType}.",
                        operation.Id,
                        operation.Type);

                    var runId = await ExecuteOperationAsync(scope.ServiceProvider, operation.Type, stoppingToken);
                    await queue.MarkCompletedAsync(operation.Id, runId, stoppingToken);
                }
                catch (Exception ex)
                {
                    await queue.MarkFailedAsync(operation.Id, ex.Message, stoppingToken);
                    logger.LogError(
                        ex,
                        "Operation request {OperationId} of type {OperationType} failed.",
                        operation.Id,
                        operation.Type);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "OperationQueueHostedService error");
                try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private static async Task<string?> ExecuteOperationAsync(
        IServiceProvider services,
        string operationType,
        CancellationToken ct)
    {
        switch (operationType)
        {
            case "full_sync":
                await services.GetRequiredService<ISyncService>().FullSyncAsync(ct);
                return null;
            case "inbox_sync":
                await services.GetRequiredService<ISyncService>().SyncInboxAsync(ct);
                return null;
            case "process_now":
                var processResult = await services.GetRequiredService<IAgentProcessService>().ProcessNowAsync(ct);
                return processResult.Execution?.RunId;
            case "categorize":
                await services.GetRequiredService<ICategorizationPipeline>().CategorizeNewVideosAsync(ct);
                return null;
            case "organize_execute":
                var result = await services.GetRequiredService<IOrganizeExecutorService>().ExecuteAsync(ct);
                return result.RunId;
            default:
                throw new InvalidOperationException($"Unsupported operation type '{operationType}'.");
        }
    }
}
