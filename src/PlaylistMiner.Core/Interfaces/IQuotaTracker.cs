namespace PlaylistMiner.Core.Interfaces;

public interface IQuotaTracker
{
    Task<bool> IsQuotaExhaustedAsync(CancellationToken ct = default);
    Task RecordQuotaExhaustedAsync(CancellationToken ct = default);
    Task<QuotaStatus> GetStatusAsync(CancellationToken ct = default);
}

public record QuotaStatus(
    bool IsExhausted,
    DateTime? ExhaustedAt,
    DateTime ResetsAt,
    string Message);
