using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Exceptions;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.Infrastructure.Services;

public class PlaylistOrganizer(
    PlaylistMinerDbContext db,
    IYouTubeApiClient youTubeApiClient,
    ILogger<PlaylistOrganizer> logger) : IPlaylistOrganizer
{
    public async Task MoveVideoAsync(
        int videoId,
        int sourcePlaylistId,
        int targetPlaylistId,
        CancellationToken ct = default)
    {
        var video = await db.Videos.FindAsync([videoId], ct)
            ?? throw new InvalidOperationException($"Video {videoId} not found.");

        var sourcePlaylist = await db.Playlists.FindAsync([sourcePlaylistId], ct)
            ?? throw new InvalidOperationException($"Source playlist {sourcePlaylistId} not found.");

        var targetPlaylist = await db.Playlists.FindAsync([targetPlaylistId], ct)
            ?? throw new InvalidOperationException($"Target playlist {targetPlaylistId} not found.");

        var sourcePlaylistVideo = await db.PlaylistVideos
            .FirstOrDefaultAsync(pv => pv.PlaylistId == sourcePlaylistId && pv.VideoId == videoId, ct);

        var playlistItemId = sourcePlaylistVideo?.PlaylistItemId ?? video.YouTubeId;

        logger.LogInformation("Moving video {VideoId} from playlist {Source} to {Target}.",
            videoId, sourcePlaylistId, targetPlaylistId);

        await youTubeApiClient.AddVideoToPlaylistAsync(targetPlaylist.YouTubeId, video.YouTubeId, ct);
        await youTubeApiClient.RemoveVideoFromPlaylistAsync(sourcePlaylist.YouTubeId, playlistItemId, ct);

        if (sourcePlaylistVideo is not null)
        {
            db.PlaylistVideos.Remove(sourcePlaylistVideo);
        }

        var targetPlaylistVideo = await db.PlaylistVideos
            .FirstOrDefaultAsync(pv => pv.PlaylistId == targetPlaylistId && pv.VideoId == videoId, ct);

        if (targetPlaylistVideo is null)
        {
            db.PlaylistVideos.Add(new PlaylistVideo
            {
                PlaylistId = targetPlaylistId,
                VideoId = videoId,
                Position = 0,
                AddedAt = DateTime.UtcNow
            });
        }

        db.UndoLogs.Add(new UndoLog
        {
            VideoId = videoId,
            Action = "Move",
            SourcePlaylistId = sourcePlaylistId,
            TargetPlaylistId = targetPlaylistId,
            PlaylistItemId = playlistItemId,
            PerformedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            Undone = false
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task UndoMoveAsync(int undoLogId, CancellationToken ct = default)
    {
        var undoLog = await db.UndoLogs
            .Include(ul => ul.Video)
            .Include(ul => ul.SourcePlaylist)
            .Include(ul => ul.TargetPlaylist)
            .FirstOrDefaultAsync(ul => ul.Id == undoLogId, ct)
            ?? throw new InvalidOperationException($"UndoLog {undoLogId} not found.");

        if (undoLog.ExpiresAt < DateTime.UtcNow)
            throw new GoneException($"UndoLog {undoLogId} has expired and cannot be undone.");

        var video = undoLog.Video;
        var sourcePlaylist = undoLog.SourcePlaylist
            ?? throw new InvalidOperationException("Source playlist not found.");
        var targetPlaylist = undoLog.TargetPlaylist
            ?? throw new InvalidOperationException("Target playlist not found.");

        logger.LogInformation("Undoing move of video {VideoId} from playlist {Target} back to {Source}.",
            video.Id, targetPlaylist.Id, sourcePlaylist.Id);

        await youTubeApiClient.AddVideoToPlaylistAsync(sourcePlaylist.YouTubeId, video.YouTubeId, ct);
        await youTubeApiClient.RemoveVideoFromPlaylistAsync(
            targetPlaylist.YouTubeId,
            undoLog.PlaylistItemId ?? video.YouTubeId,
            ct);

        var targetPv = await db.PlaylistVideos
            .FirstOrDefaultAsync(pv => pv.PlaylistId == targetPlaylist.Id && pv.VideoId == video.Id, ct);
        if (targetPv is not null)
            db.PlaylistVideos.Remove(targetPv);

        var sourcePv = await db.PlaylistVideos
            .FirstOrDefaultAsync(pv => pv.PlaylistId == sourcePlaylist.Id && pv.VideoId == video.Id, ct);
        if (sourcePv is null)
        {
            db.PlaylistVideos.Add(new PlaylistVideo
            {
                PlaylistId = sourcePlaylist.Id,
                VideoId = video.Id,
                Position = 0,
                AddedAt = DateTime.UtcNow
            });
        }

        undoLog.Undone = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<PlaylistDto>> ConsolidateAsync(CancellationToken ct = default)
    {
        return await db.Playlists
            .AsNoTracking()
            .Select(p => new PlaylistDto(
                p.YouTubeId,
                p.Name,
                p.Description,
                p.IsInbox,
                p.PlaylistVideos.Count,
                p.Id))
            .ToListAsync(ct);
    }
}
