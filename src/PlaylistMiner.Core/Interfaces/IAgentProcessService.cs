using PlaylistMiner.Core.DTOs;

namespace PlaylistMiner.Core.Interfaces;

public interface IAgentProcessService
{
    Task<AgentProcessResultDto> ProcessNowAsync(CancellationToken ct = default);
}
