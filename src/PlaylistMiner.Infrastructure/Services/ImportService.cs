using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Import;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.Infrastructure.Services;

public class ImportService(
    PlaylistMinerDbContext db,
    IYouTubeApiClient youTubeApiClient,
    ILogger<ImportService> logger) : IImportService
{
    public async Task<ImportResult> ImportTakeoutAsync(Stream csvStream, CancellationToken ct = default)
    {
        List<TakeoutEntry> entries;
        try
        {
            entries = TakeoutParser.Parse(csvStream);
        }
        catch (ImportException)
        {
            return new ImportResult(0, 0, 0, 1, string.Empty);
        }

        var existingIds = await db.Videos
            .Where(v => entries.Select(e => e.VideoId).Contains(v.YouTubeId))
            .Select(v => v.YouTubeId)
            .ToListAsync(ct);

        var existingSet = existingIds.ToHashSet();
        var newEntries = entries.Where(e => !existingSet.Contains(e.VideoId)).ToList();
        var skipped = entries.Count - newEntries.Count;
        var imported = 0;
        var errors = 0;
        var now = DateTime.UtcNow;

        foreach (var batch in newEntries.Chunk(50))
        {
            try
            {
                var metadata = await youTubeApiClient.GetVideoMetadataAsync(
                    batch.Select(e => e.VideoId), ct);

                var metaMap = metadata.ToDictionary(m => m.YouTubeId);

                foreach (var entry in batch)
                {
                    if (metaMap.TryGetValue(entry.VideoId, out var meta))
                    {
                        db.Videos.Add(new Video
                        {
                            YouTubeId = meta.YouTubeId,
                            Title = meta.Title,
                            Description = meta.Description,
                            ChannelName = meta.ChannelName,
                            ChannelId = meta.ChannelId,
                            ThumbnailUrl = meta.ThumbnailUrl,
                            Duration = meta.Duration,
                            PublishedAt = meta.PublishedAt,
                            Status = meta.Status,
                            CreatedAt = now,
                            UpdatedAt = now,
                            SyncedAt = now
                        });
                        imported++;
                    }
                    else
                    {
                        db.Videos.Add(new Video
                        {
                            YouTubeId = entry.VideoId,
                            Title = $"[Unavailable] {entry.VideoId}",
                            Description = string.Empty,
                            ChannelName = string.Empty,
                            ChannelId = string.Empty,
                            ThumbnailUrl = string.Empty,
                            Status = VideoStatus.Unavailable,
                            CreatedAt = now,
                            UpdatedAt = now,
                            SyncedAt = now
                        });
                        imported++;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to hydrate batch of {Count} videos.", batch.Length);
                errors += batch.Length;
            }
        }

        if (imported > 0)
            await db.SaveChangesAsync(ct);

        var importBatch = new ImportBatch
        {
            Source = "Takeout",
            Filename = $"takeout-{now:yyyyMMddHHmmss}",
            TotalVideos = entries.Count,
            ImportedCount = imported,
            FailedCount = errors,
            ImportedAt = now
        };
        db.ImportBatches.Add(importBatch);
        await db.SaveChangesAsync(ct);

        return new ImportResult(entries.Count, imported, skipped, errors, importBatch.Id.ToString());
    }

    public async Task<ImportResult> ImportTakeoutFromPathAsync(string folderPath, CancellationToken ct = default)
    {
        var csvPath = FindTakeoutCsv(folderPath);
        await using var stream = File.OpenRead(csvPath);
        return await ImportTakeoutAsync(stream, ct);
    }

    public async Task<List<ImportBatch>> GetHistoryAsync(CancellationToken ct = default)
    {
        return await db.ImportBatches
            .AsNoTracking()
            .OrderByDescending(b => b.ImportedAt)
            .ToListAsync(ct);
    }

    private static string FindTakeoutCsv(string folderPath)
    {
        var patterns = new[]
        {
            "Watch later-videos.csv",
            "watch-later-videos.csv",
            "**/YouTube and YouTube Music/playlists/Watch later-videos.csv"
        };

        foreach (var pattern in patterns)
        {
            if (pattern.Contains("**"))
            {
                var files = Directory.GetFiles(folderPath, Path.GetFileName(pattern), SearchOption.AllDirectories);
                if (files.Length > 0)
                    return files[0];
            }
            else
            {
                var path = Path.Combine(folderPath, pattern);
                if (File.Exists(path))
                    return path;
            }
        }

        throw new ImportException(
            $"Could not find Watch Later CSV in '{folderPath}'. Expected 'Watch later-videos.csv'.");
    }
}
