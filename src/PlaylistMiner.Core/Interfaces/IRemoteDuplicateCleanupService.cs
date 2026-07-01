using PlaylistMiner.Core.DTOs;

namespace PlaylistMiner.Core.Interfaces;

public interface IRemoteDuplicateCleanupService
{
    Task<List<RemoteDuplicateCleanupItemDto>> BuildPlanAsync(CancellationToken ct = default);
    Task<RemoteDuplicateCleanupResultDto> ExecuteAsync(IEnumerable<RemoteDuplicateCleanupItemDto> plan, CancellationToken ct = default);
}
