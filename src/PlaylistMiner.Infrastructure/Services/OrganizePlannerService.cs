using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using PlaylistMiner.Core.Categorization;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.Infrastructure.Services;

public sealed class OrganizePlannerService(
    PlaylistMinerDbContext db,
    IOptions<CategorizationOptions> options,
    ILogger<OrganizePlannerService> logger) : IOrganizePlannerService
{
    public async Task<OrganizePlanDto> BuildPlanAsync(CancellationToken ct = default)
    {
        var confidenceThreshold = options.Value.AutoFileConfidence;
        var inbox = await db.Playlists
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IsInbox, ct);

        if (inbox is null)
        {
            logger.LogWarning("No inbox playlist is configured. Returning an empty organize plan.");
            return new OrganizePlanDto(0, 0, 0, []);
        }

        var managedPlaylists = await db.Playlists
            .AsNoTracking()
            .Where(p => p.IsManaged && p.Topic != null)
            .ToListAsync(ct);

        var inboxVideos = await db.PlaylistVideos
            .AsNoTracking()
            .Where(pv => pv.PlaylistId == inbox.Id)
            .Select(pv => new
            {
                pv.VideoId,
                pv.Video.YouTubeId,
                pv.Video.Title,
                Tags = pv.Video.VideoTags.Select(vt => new
                {
                    vt.Tag.Name,
                    vt.Source,
                    vt.Confidence
                }).ToList()
            })
            .ToListAsync(ct);

        var items = new List<OrganizePlanItemDto>();
        var plannedPlaylistCreations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var video in inboxVideos.OrderBy(v => v.Title, StringComparer.OrdinalIgnoreCase))
        {
            var bestTopic = video.Tags
                .Select(tag => new
                {
                    Topic = tag.Name,
                    Confidence = GetEffectiveConfidence(tag.Source, tag.Confidence),
                    tag.Source
                })
                .OrderByDescending(tag => tag.Confidence)
                .ThenBy(tag => tag.Topic, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (bestTopic is null || bestTopic.Confidence < confidenceThreshold)
            {
                items.Add(new OrganizePlanItemDto(
                    "review",
                    video.VideoId,
                    video.YouTubeId,
                    video.Title,
                    inbox.Name,
                    null,
                    null,
                    bestTopic?.Topic,
                    bestTopic?.Confidence,
                    0,
                    "Best available topic confidence is below threshold."));
                continue;
            }

            var managedPlaylist = managedPlaylists
                .FirstOrDefault(p => string.Equals(p.Topic, bestTopic.Topic, StringComparison.OrdinalIgnoreCase));

            if (managedPlaylist is null && plannedPlaylistCreations.Add(bestTopic.Topic))
            {
                items.Add(new OrganizePlanItemDto(
                    "create_playlist",
                    null,
                    null,
                    null,
                    null,
                    bestTopic.Topic,
                    null,
                    bestTopic.Topic,
                    null,
                    50,
                    "Managed playlist does not exist yet."));
            }

            items.Add(new OrganizePlanItemDto(
                "move",
                video.VideoId,
                video.YouTubeId,
                video.Title,
                inbox.Name,
                managedPlaylist?.Name ?? bestTopic.Topic,
                managedPlaylist?.Id,
                bestTopic.Topic,
                bestTopic.Confidence,
                100,
                "Best topic confidence is above threshold."));
        }

        return new OrganizePlanDto(
            inboxVideos.Count,
            items.Count,
            items.Sum(item => item.EstimatedQuotaCost),
            items);
    }

    private static float GetEffectiveConfidence(TagSource source, float? confidence)
    {
        if (source == TagSource.Manual)
        {
            return 1.0f;
        }

        return confidence ?? 0.0f;
    }
}
