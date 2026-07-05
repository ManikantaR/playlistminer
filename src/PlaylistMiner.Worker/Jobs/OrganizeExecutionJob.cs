using Microsoft.Extensions.Logging;
using PlaylistMiner.Core.Interfaces;
using Quartz;

namespace PlaylistMiner.Worker.Jobs;

[DisallowConcurrentExecution]
public class OrganizeExecutionJob(
    IOllamaCategorizer ollamaCategorizer,
    IOrganizeExecutorService organizeExecutorService,
    ILogger<OrganizeExecutionJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            if (!await ollamaCategorizer.IsAvailableAsync(context.CancellationToken))
            {
                logger.LogInformation("Skipping organize execution because Ollama is unavailable.");
                return;
            }

            var result = await organizeExecutorService.ExecuteAsync(context.CancellationToken);
            logger.LogInformation(
                "Organize execution completed. Planned {MovesPlanned}, executed {MovesExecuted}, deferred {DeferredCount}, run {RunId}.",
                result.MovesPlanned,
                result.MovesExecuted,
                result.DeferredCount,
                result.RunId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Organize execution job failed");
        }
    }
}
