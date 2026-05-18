using Microsoft.Extensions.Logging;
using PlaylistMiner.Core.Interfaces;
using Quartz;

namespace PlaylistMiner.Worker.Jobs;

[DisallowConcurrentExecution]
public class CategorizationJob(ICategorizationPipeline pipeline, ILogger<CategorizationJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await pipeline.CategorizeNewVideosAsync(context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Categorization job failed");
        }
    }
}
