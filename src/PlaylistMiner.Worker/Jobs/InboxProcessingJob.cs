using Microsoft.Extensions.Logging;
using PlaylistMiner.Core.Interfaces;
using Quartz;

namespace PlaylistMiner.Worker.Jobs;

[DisallowConcurrentExecution]
public class InboxProcessingJob(
    ISyncService syncService,
    ICategorizationPipeline pipeline,
    IOllamaCategorizer ollamaCategorizer,
    ILogger<InboxProcessingJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            if (!await ollamaCategorizer.IsAvailableAsync(context.CancellationToken))
            {
                logger.LogInformation("Skipping inbox processing because Ollama is unavailable.");
                return;
            }

            await syncService.SyncInboxAsync(context.CancellationToken);
            await pipeline.CategorizeNewVideosAsync(context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Inbox processing failed");
        }
    }
}
