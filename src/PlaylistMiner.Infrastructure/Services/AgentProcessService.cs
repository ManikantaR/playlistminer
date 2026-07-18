using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;

namespace PlaylistMiner.Infrastructure.Services;

public class AgentProcessService(
    ISyncService syncService,
    ICategorizationPipeline categorizationPipeline,
    IOrganizeExecutorService organizeExecutorService,
    IOllamaCategorizer ollamaCategorizer) : IAgentProcessService
{
    public async Task<AgentProcessResultDto> ProcessNowAsync(CancellationToken ct = default)
    {
        if (!await ollamaCategorizer.IsAvailableAsync(ct))
        {
            return new AgentProcessResultDto(
                "skipped",
                "Ollama is unavailable. Incoming videos were left queued.",
                null,
                null);
        }

        var sync = await syncService.SyncInboxAsync(ct);
        await categorizationPipeline.CategorizeNewVideosAsync(ct);
        var execution = await organizeExecutorService.ExecuteAsync(ct);

        return new AgentProcessResultDto(
            "completed",
            "Processed incoming playlist now.",
            sync,
            execution);
    }
}
