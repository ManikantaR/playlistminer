using Microsoft.Extensions.Logging;
using PlaylistMiner.Core.Interfaces;
using Quartz;

namespace PlaylistMiner.Worker.Jobs;

[DisallowConcurrentExecution]
public class InboxProcessingJob(
    ISyncService syncService,
    ICategorizationPipeline pipeline,
    ILogger<InboxProcessingJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await syncService.SyncInboxAsync(context.CancellationToken);
            await pipeline.CategorizeNewVideosAsync(context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Inbox processing failed");
        }
    }
}
