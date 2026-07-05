using PlaylistMiner.Core.DTOs;

namespace PlaylistMiner.Core.Interfaces;

public interface IOrganizeExecutorService
{
    Task<OrganizeExecutionResultDto> ExecuteAsync(CancellationToken ct = default);
}
