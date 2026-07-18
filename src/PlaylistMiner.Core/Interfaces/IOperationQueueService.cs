using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Models;

namespace PlaylistMiner.Core.Interfaces;

public interface IOperationQueueService
{
    Task<OperationRequestDto> QueueAsync(CreateOperationRequestDto request, string createdBy = "user", CancellationToken ct = default);
    Task<IReadOnlyList<OperationRequestDto>> ListAsync(CancellationToken ct = default);
    Task<OperationRequestDto?> GetAsync(int id, CancellationToken ct = default);
    Task<OperationRequestDto?> CancelAsync(int id, CancellationToken ct = default);
    Task<OperationRequest?> GetNextRunnableAsync(DateTime now, CancellationToken ct = default);
    Task MarkCompletedAsync(int id, string? runId = null, CancellationToken ct = default);
    Task MarkFailedAsync(int id, string error, CancellationToken ct = default);
}
