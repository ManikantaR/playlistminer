using Microsoft.Extensions.Logging;
using PlaylistMiner.Core.Interfaces;
using Quartz;

namespace PlaylistMiner.Worker.Jobs;

[DisallowConcurrentExecution]
public class OrganizeExecutionJob(
    IOllamaCategorizer ollamaCategorizer,
    IOrganizeExecutorService organizeExecutorService,
    IAutomationPolicyService automationPolicyService,
    ILogger<OrganizeExecutionJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var policy = await automationPolicyService.GetPolicyAsync(context.CancellationToken);
            if (policy.IsPaused)
            {
                logger.LogInformation("Skipping organize execution because automation is paused.");
                return;
            }

            if (policy.Mode != "aggressive_with_undo")
            {
                logger.LogInformation(
                    "Skipping organize execution because automation mode is {AutomationMode}.",
                    policy.Mode);
                return;
            }

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
