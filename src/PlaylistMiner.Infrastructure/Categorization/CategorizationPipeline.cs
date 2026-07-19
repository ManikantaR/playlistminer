namespace PlaylistMiner.Infrastructure.Categorization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlaylistMiner.Core.Categorization;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;

public class CategorizationPipeline(
    IKeywordMatcher keywordMatcher,
    ITfIdfScorer tfIdfScorer,
    IOllamaCategorizer ollamaCategorizer,
    PlaylistMinerDbContext db,
    IPipelineRunTracker tracker,
    IOptions<CategorizationOptions> options,
    ILogger<CategorizationPipeline> logger) : ICategorizationPipeline
{
    public async Task<List<TagSuggestion>> ClassifyAsync(int videoId, CancellationToken ct = default)
    {
        var video = await db.Videos
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == videoId, ct);

        if (video is null)
        {
            logger.LogWarning("Video {VideoId} not found for classification", videoId);
            return [];
        }

        return await ClassifyVideoAsync(video, null, ct);
    }

    public async Task<List<TagSuggestion>> CategorizeAsync(int videoId, string? runId = null, CancellationToken ct = default)
    {
        var video = await db.Videos
            .Include(v => v.VideoTags)
            .FirstOrDefaultAsync(v => v.Id == videoId, ct);

        if (video is null)
        {
            logger.LogWarning("Video {VideoId} not found", videoId);
            return [];
        }

        // Skip videos that already have manual tags
        if (video.VideoTags.Any(vt => vt.Source == TagSource.Manual))
        {
            logger.LogInformation("Video {VideoId} already has manual tags; skipping", videoId);
            return [];
        }

        var merged = await ClassifyVideoAsync(video, runId, ct);

        if (merged.Count == 0)
        {
            if (runId is not null)
            {
                await tracker.UpdateRunAsync(runId, r => {
                    r.VideosProcessed++;
                    r.VideosSkipped++;
                }, message: $"No tag suggestions found for video {videoId}.", ct: ct);
            }
            return [];
        }

        // Save suggestions as VideoTag records (skip existing)
        var existingKeys = video.VideoTags
            .Select(vt => (vt.TagId, vt.Source))
            .ToHashSet();

        var toAdd = merged
            .Where(s => !existingKeys.Contains((s.TagId, s.Source)))
            .Select(s => new VideoTag
            {
                VideoId = videoId,
                TagId = s.TagId,
                Source = s.Source,
                Confidence = s.Confidence,
                CreatedAt = DateTime.UtcNow
            })
            .ToList();

        if (runId is not null)
        {
            await tracker.UpdateRunAsync(runId, _ => {}, phase: "writing_suggestions", message: $"Writing {toAdd.Count} tag suggestions for video {videoId}...", ct: ct);
        }

        if (toAdd.Count > 0)
        {
            db.VideoTags.AddRange(toAdd);
            await db.SaveChangesAsync(ct);
        }

        if (runId is not null)
        {
            int ruleHits = merged.Count(s => s.Source == TagSource.RuleBased);
            int tfidfHits = merged.Count(s => s.Source == TagSource.TfIdf);
            int ollamaHits = merged.Count(s => s.Source == TagSource.Ollama);

            await tracker.UpdateRunAsync(runId, r => {
                r.VideosProcessed++;
                r.VideosTagged++;
                r.RuleBasedHits += ruleHits;
                r.TfidfHits += tfidfHits;
                r.OllamaHits += ollamaHits;
            }, message: $"Saved {toAdd.Count} tag suggestions for video {videoId}.", ct: ct);
        }

        logger.LogInformation("Video {VideoId}: saved {Count} tag suggestions", videoId, toAdd.Count);
        return merged;
    }

    public async Task CategorizeNewVideosAsync(CancellationToken ct = default)
    {
        var runId = await tracker.StartRunAsync("categorization", ct);

        try
        {
            await tracker.UpdateRunAsync(runId, _ => {}, phase: "loading_candidates", message: "Loading candidate videos pending tagging...", ct: ct);
            var newVideoIds = await db.Videos
                .Where(v => v.Status == VideoStatus.Active && !v.VideoTags.Any())
                .Select(v => v.Id)
                .ToListAsync(ct);

            await tracker.UpdateRunAsync(runId, r => {
                r.VideosPendingTagging = newVideoIds.Count;
            }, message: $"Found {newVideoIds.Count} videos pending tagging.", ct: ct);

            var maxVideosPerRun = Math.Max(1, options.Value.MaxVideosPerRun);
            var batchVideoIds = newVideoIds.Take(maxVideosPerRun).ToList();

            await tracker.UpdateRunAsync(
                runId,
                _ => { },
                phase: "categorizing",
                message: $"Categorizing {batchVideoIds.Count} of {newVideoIds.Count} pending videos this run.",
                ct: ct);

            foreach (var videoId in batchVideoIds)
            {
                try
                {
                    await CategorizeAsync(videoId, runId, ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to categorize video {VideoId}", videoId);
                    await tracker.UpdateRunAsync(runId, r => {
                        r.VideosProcessed++;
                        r.ErrorsCount++;
                    }, message: $"Error categorizing video {videoId}: {ex.Message}", ct: ct);
                }
            }

            await tracker.UpdateRunAsync(runId, _ => {}, phase: "finalizing", message: "Finalizing categorization run...", ct: ct);
            await tracker.CompleteRunAsync(runId, null, ct);
        }
        catch (Exception ex)
        {
            await tracker.FailRunAsync(runId, ex.Message, null, ct);
            throw;
        }
    }

    public async Task ReclassifyGeneratedAsync(CancellationToken ct = default)
    {
        var runId = await tracker.StartRunAsync("reclassification", ct);

        try
        {
            await tracker.UpdateRunAsync(runId, _ => { }, phase: "loading_candidates", message: "Loading active videos for generated-tag rebuild...", ct: ct);

            var activeVideoIds = await db.Videos
                .Where(v => v.Status == VideoStatus.Active)
                .Select(v => v.Id)
                .ToListAsync(ct);

            var manualVideoIds = await db.VideoTags
                .Where(vt => activeVideoIds.Contains(vt.VideoId) && vt.Source == TagSource.Manual)
                .Select(vt => vt.VideoId)
                .Distinct()
                .ToListAsync(ct);

            var manualVideoIdSet = manualVideoIds.ToHashSet();
            var candidateIds = activeVideoIds
                .Where(id => !manualVideoIdSet.Contains(id))
                .ToList();

            await tracker.UpdateRunAsync(runId, r =>
            {
                r.VideosPendingTagging = candidateIds.Count;
                r.VideosSkipped = manualVideoIds.Count;
            }, phase: "clearing_generated_tags", message: "Clearing generated tag suggestions while preserving manual tags...", ct: ct);

            var generatedTags = await db.VideoTags
                .Where(vt => activeVideoIds.Contains(vt.VideoId) && vt.Source != TagSource.Manual)
                .ToListAsync(ct);

            if (generatedTags.Count > 0)
            {
                db.VideoTags.RemoveRange(generatedTags);
                await db.SaveChangesAsync(ct);
            }

            await tracker.UpdateRunAsync(runId, _ => { }, phase: "reclassifying", message: $"Reclassifying {candidateIds.Count} active videos...", ct: ct);

            foreach (var videoId in candidateIds)
            {
                try
                {
                    await CategorizeAsync(videoId, runId, ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to reclassify video {VideoId}", videoId);
                    await tracker.UpdateRunAsync(runId, r =>
                    {
                        r.VideosProcessed++;
                        r.ErrorsCount++;
                    }, message: $"Error reclassifying video {videoId}: {ex.Message}", ct: ct);
                }
            }

            await tracker.UpdateRunAsync(runId, _ => { }, phase: "finalizing", message: "Finalizing reclassification run...", ct: ct);
            await tracker.CompleteRunAsync(runId, null, ct);
        }
        catch (Exception ex)
        {
            await tracker.FailRunAsync(runId, ex.Message, null, ct);
            throw;
        }
    }

    private async Task<List<TagSuggestion>> ClassifyVideoAsync(Video video, string? runId, CancellationToken ct)
    {
        var context = new VideoContext(video.Title, video.Description ?? string.Empty);
        var allTags = await db.Tags
            .AsNoTracking()
            .ToListAsync(ct);
        var tagByName = allTags.ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);

        if (await ollamaCategorizer.IsAvailableAsync(ct))
        {
            if (runId is not null)
            {
                await tracker.UpdateRunAsync(runId, _ => { }, phase: "ollama_classification", message: $"Running Ollama classification for video {video.Id}...", ct: ct);
            }

            var ollamaSuggestions = await ollamaCategorizer.CategorizeAsync(context, allTags.Select(t => t.Name), ct);
            var resolved = ollamaSuggestions
                .Where(s => tagByName.TryGetValue(s.TagName, out _))
                .Select(s => s with { TagId = tagByName[s.TagName].Id })
                .OrderByDescending(s => s.Confidence)
                .ThenBy(s => s.TagName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (resolved.Count > 0)
            {
                return resolved;
            }

            logger.LogWarning("Ollama returned no usable classifications for video {VideoId}; falling back to keyword/TF-IDF.", video.Id);
        }
        else
        {
            logger.LogWarning("Ollama unavailable for video {VideoId}; falling back to keyword/TF-IDF.", video.Id);
        }

        if (runId is not null)
        {
            await tracker.UpdateRunAsync(runId, _ => { }, phase: "rule_matching", message: $"Running keyword matching and TF-IDF scoring for video {video.Id}...", ct: ct);
        }

        var keywordTask = keywordMatcher.MatchAsync(context, ct);
        var tfidfTask = tfIdfScorer.ScoreAsync(context, ct);
        await Task.WhenAll(keywordTask, tfidfTask);

        return keywordTask.Result
            .Concat(tfidfTask.Result)
            .GroupBy(s => s.TagId)
            .Select(g => g.MaxBy(s => s.Confidence)!)
            .OrderByDescending(s => s.Confidence)
            .ThenBy(s => s.TagName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
