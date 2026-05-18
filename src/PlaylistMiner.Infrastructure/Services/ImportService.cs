using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.Infrastructure.Services;

public class ImportService(PlaylistMinerDbContext db, ILogger<ImportService> logger) : IImportService
{
    public async Task<ImportResult> ImportTakeoutAsync(Stream csvStream, CancellationToken ct = default)
    {
        var batchId = Guid.NewGuid().ToString("N");
        var videosFound = 0;
        var videosImported = 0;
        var skipped = 0;
        var errors = 0;

        using var reader = new StreamReader(csvStream);
        string? line = await reader.ReadLineAsync(ct);

        if (line is null)
        {
            return new ImportResult(0, 0, 0, 1, batchId);
        }

        // Validate header
        var header = line.Split(',');
        if (header.Length < 1 || !header[0].Trim().Equals("Video ID", StringComparison.OrdinalIgnoreCase))
        {
            return new ImportResult(0, 0, 0, 1, batchId);
        }

        var now = DateTime.UtcNow;

        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',');
            if (parts.Length < 1 || string.IsNullOrWhiteSpace(parts[0]))
            {
                errors++;
                continue;
            }

            var youtubeId = parts[0].Trim();
            videosFound++;

            try
            {
                var existing = await db.Videos
                    .FirstOrDefaultAsync(v => v.YouTubeId == youtubeId, ct);

                if (existing is not null)
                {
                    skipped++;
                    continue;
                }

                db.Videos.Add(new Video
                {
                    YouTubeId = youtubeId,
                    Title = $"[Imported] {youtubeId}",
                    Description = string.Empty,
                    ChannelName = string.Empty,
                    ChannelId = string.Empty,
                    ThumbnailUrl = string.Empty,
                    Status = VideoStatus.Active,
                    CreatedAt = now,
                    UpdatedAt = now,
                    SyncedAt = now
                });

                videosImported++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to import video {YouTubeId}.", youtubeId);
                errors++;
            }
        }

        if (videosImported > 0)
            await db.SaveChangesAsync(ct);

        var batch = new ImportBatch
        {
            Source = "Takeout",
            Filename = batchId,
            TotalVideos = videosFound,
            ImportedCount = videosImported,
            FailedCount = errors,
            ImportedAt = now
        };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync(ct);

        return new ImportResult(videosFound, videosImported, skipped, errors, batchId);
    }

    public async Task<List<ImportBatch>> GetHistoryAsync(CancellationToken ct = default)
    {
        return await db.ImportBatches
            .AsNoTracking()
            .OrderByDescending(b => b.ImportedAt)
            .ToListAsync(ct);
    }
}
