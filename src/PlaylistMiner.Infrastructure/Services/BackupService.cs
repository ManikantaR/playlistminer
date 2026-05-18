using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.Infrastructure.Services;

public class BackupService(
    PlaylistMinerDbContext db,
    IConfiguration configuration,
    IProcessRunner processRunner,
    ILogger<BackupService> logger) : IBackupService
{
    private string BackupDirectory =>
        configuration["Backup:Directory"] ?? "./data/backups/";

    public async Task<BackupResult> TriggerBackupAsync(CancellationToken ct = default)
    {
        var backupDir = BackupDirectory;
        Directory.CreateDirectory(backupDir);

        var filename = $"playlistminer_{DateTime.UtcNow:yyyyMMdd_HHmmss}.sql";
        var outputPath = Path.Combine(backupDir, filename);

        var connectionString = configuration.GetConnectionString("playlistminer") ?? string.Empty;
        var args = BuildPgDumpArgs(connectionString);

        try
        {
            var exitCode = await processRunner.RunAsync("pg_dump", args, outputPath, ct);
            if (exitCode != 0)
                throw new InvalidOperationException($"pg_dump exited with code {exitCode}");

            long sizeBytes = 0;
            try { sizeBytes = new FileInfo(outputPath).Length; } catch { /* file may not exist in tests */ }

            var log = new BackupLog
            {
                Filename = filename,
                SizeBytes = sizeBytes,
                CreatedAt = DateTime.UtcNow,
                Status = "success"
            };
            db.BackupLogs.Add(log);
            await db.SaveChangesAsync(ct);

            return new BackupResult(filename, sizeBytes, true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Backup failed");

            var log = new BackupLog
            {
                Filename = filename,
                SizeBytes = 0,
                CreatedAt = DateTime.UtcNow,
                Status = "failed",
                Error = ex.Message
            };
            db.BackupLogs.Add(log);
            await db.SaveChangesAsync(ct);

            return new BackupResult(filename, 0, false, ex.Message);
        }
    }

    public async Task<List<BackupInfo>> ListBackupsAsync(CancellationToken ct = default)
    {
        var logs = await db.BackupLogs
            .OrderByDescending(bl => bl.CreatedAt)
            .ToListAsync(ct);

        return logs.Select(bl => new BackupInfo(bl.Filename, bl.SizeBytes, bl.CreatedAt, bl.Status)).ToList();
    }

    public Task<Stream> GetBackupStreamAsync(string filename, CancellationToken ct = default)
    {
        var path = Path.Combine(BackupDirectory, filename);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Backup file not found: {filename}", path);

        Stream stream = File.OpenRead(path);
        return Task.FromResult(stream);
    }

    public async Task CleanupOldBackupsAsync(int keepDays = 7, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-keepDays);

        var oldLogs = await db.BackupLogs
            .Where(bl => bl.CreatedAt < cutoff)
            .ToListAsync(ct);

        foreach (var log in oldLogs)
        {
            try
            {
                var path = Path.Combine(BackupDirectory, log.Filename);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not delete backup file {Filename}", log.Filename);
            }
        }

        db.BackupLogs.RemoveRange(oldLogs);
        await db.SaveChangesAsync(ct);
    }

    private static string BuildPgDumpArgs(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return string.Empty;

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var args = new List<string>();
            if (!string.IsNullOrEmpty(builder.Host)) args.Add($"--host={builder.Host}");
            if (builder.Port != 5432) args.Add($"--port={builder.Port}");
            if (!string.IsNullOrEmpty(builder.Database)) args.Add($"--dbname={builder.Database}");
            if (!string.IsNullOrEmpty(builder.Username)) args.Add($"--username={builder.Username}");
            return string.Join(" ", args);
        }
        catch
        {
            return string.Empty;
        }
    }
}
