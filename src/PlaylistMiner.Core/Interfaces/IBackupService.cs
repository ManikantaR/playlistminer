namespace PlaylistMiner.Core.Interfaces;

public record BackupResult(string Filename, long SizeBytes, bool Success, string? Error);
public record BackupInfo(string Filename, long SizeBytes, DateTime CreatedAt, string Status);

public interface IBackupService
{
    Task<BackupResult> TriggerBackupAsync(CancellationToken ct = default);
    Task<List<BackupInfo>> ListBackupsAsync(CancellationToken ct = default);
    Task<Stream> GetBackupStreamAsync(string filename, CancellationToken ct = default);
    Task CleanupOldBackupsAsync(int keepDays = 7, CancellationToken ct = default);
}
