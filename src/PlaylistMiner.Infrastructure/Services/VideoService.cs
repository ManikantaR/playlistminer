using Microsoft.EntityFrameworkCore;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.Infrastructure.Services;

public class VideoService(PlaylistMinerDbContext db, ISelfLearningService selfLearning) : IVideoService
{
    public async Task AcceptTagAsync(int videoId, int tagId, CancellationToken ct = default)
    {
        // Source is part of the composite key — must delete the existing row and add a new Manual one
        var existing = await db.VideoTags
            .FirstOrDefaultAsync(vt => vt.VideoId == videoId && vt.TagId == tagId, ct);

        if (existing is not null && existing.Source != TagSource.Manual)
        {
            db.VideoTags.Remove(existing);
            db.VideoTags.Add(new VideoTag
            {
                VideoId = videoId,
                TagId = tagId,
                Source = TagSource.Manual,
                CreatedAt = existing.CreatedAt
            });
            await db.SaveChangesAsync(ct);
        }
        else if (existing is null)
        {
            db.VideoTags.Add(new VideoTag
            {
                VideoId = videoId,
                TagId = tagId,
                Source = TagSource.Manual,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }

        await selfLearning.OnTagAcceptedAsync(videoId, tagId, ct);
    }

    public async Task RejectTagAsync(int videoId, int tagId, CancellationToken ct = default)
    {
        var videoTag = await db.VideoTags
            .FirstOrDefaultAsync(vt => vt.VideoId == videoId && vt.TagId == tagId && vt.Source != TagSource.Manual, ct);

        if (videoTag is not null)
        {
            db.VideoTags.Remove(videoTag);
            await db.SaveChangesAsync(ct);
        }

        await selfLearning.OnTagRejectedAsync(videoId, tagId, ct);
    }

    public async Task AddTagAsync(int videoId, int tagId, CancellationToken ct = default)
    {
        var exists = await db.VideoTags
            .AnyAsync(vt => vt.VideoId == videoId && vt.TagId == tagId && vt.Source == TagSource.Manual, ct);

        if (!exists)
        {
            db.VideoTags.Add(new VideoTag
            {
                VideoId = videoId,
                TagId = tagId,
                Source = TagSource.Manual,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task RemoveTagAsync(int videoId, int tagId, CancellationToken ct = default)
    {
        var videoTags = await db.VideoTags
            .Where(vt => vt.VideoId == videoId && vt.TagId == tagId)
            .ToListAsync(ct);

        if (videoTags.Count > 0)
        {
            db.VideoTags.RemoveRange(videoTags);
            await db.SaveChangesAsync(ct);
        }
    }
}
