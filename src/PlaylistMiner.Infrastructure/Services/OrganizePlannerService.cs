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
    private sealed record TopicCandidate(string Topic, float Confidence);

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
            var rankedTopics = video.Tags
                .Select(tag => new TopicCandidate(
                    tag.Name,
                    GetEffectiveConfidence(tag.Source, tag.Confidence)))
                .OrderByDescending(tag => tag.Confidence)
                .ThenBy(tag => tag.Topic, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var bestTopic = rankedTopics.FirstOrDefault();
            var qualifyingTopics = rankedTopics
                .Where(tag => tag.Confidence >= confidenceThreshold)
                .ToList();

            if (qualifyingTopics.Count == 0 || bestTopic is null)
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
                BuildMoveReason(bestTopic.Topic, qualifyingTopics)));
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

    private static string BuildMoveReason(
        string winningTopic,
        IReadOnlyList<TopicCandidate> qualifyingTopics)
    {
        if (qualifyingTopics.Count <= 1)
        {
            return "Best topic confidence is above threshold.";
        }

        var secondaryTopics = qualifyingTopics
            .Skip(1)
            .Select(topic => topic.Topic)
            .ToList();

        return $"Multiple topics cleared the threshold, but the single topic filing policy chose \"{winningTopic}\" and deferred secondary topics ({string.Join(", ", secondaryTopics)}).";
    }
}
