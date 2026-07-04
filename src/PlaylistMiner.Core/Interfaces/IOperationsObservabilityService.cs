using PlaylistMiner.Core.DTOs;

namespace PlaylistMiner.Core.Interfaces;

public interface IOperationsObservabilityService
{
    Task<OperationsActivityFeedDto> GetActivityAsync(int limit, int offset, CancellationToken ct = default);
    Task<OperationsQuotaDto> GetMoveBudgetAsync(CancellationToken ct = default);
}
