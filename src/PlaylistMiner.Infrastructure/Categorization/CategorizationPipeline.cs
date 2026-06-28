namespace PlaylistMiner.Infrastructure.Categorization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    ILogger<CategorizationPipeline> logger) : ICategorizationPipeline
{
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

        if (runId is not null)
        {
            await tracker.UpdateRunAsync(runId, _ => {}, phase: "rule_matching", message: $"Running keyword matching and TF-IDF scoring for video {videoId}...", ct: ct);
        }

        var context = new VideoContext(video.Title, video.Description);

        // Run keyword matcher and TF-IDF in parallel for efficiency
        var keywordTask = keywordMatcher.MatchAsync(context, ct);
        var tfidfTask = tfIdfScorer.ScoreAsync(context, ct);

        await Task.WhenAll(keywordTask, tfidfTask);

        var keywordResults = keywordTask.Result;
        var tfidfResults = tfidfTask.Result;

        // Merge: group by TagId, keep highest confidence
        var merged = keywordResults
            .Concat(tfidfResults)
            .GroupBy(s => s.TagId)
            .Select(g => g.MaxBy(s => s.Confidence)!)
            .ToList();

        // If merged is empty, try Ollama as fallback
        if (merged.Count == 0)
        {
            if (await ollamaCategorizer.IsAvailableAsync(ct))
            {
                if (runId is not null)
                {
                    await tracker.UpdateRunAsync(runId, _ => {}, phase: "ollama_classification", message: $"Running Ollama classification for video {videoId}...", ct: ct);
                }

                var allTags = await db.Tags.ToListAsync(ct);
                var availableTagNames = allTags.Select(t => t.Name);
                var ollamaSuggestions = await ollamaCategorizer.CategorizeAsync(context, availableTagNames, ct);

                // Resolve tag names to IDs
                var tagByName = allTags.ToDictionary(
                    t => t.Name,
                    t => t,
                    StringComparer.OrdinalIgnoreCase);

                foreach (var suggestion in ollamaSuggestions)
                {
                    if (tagByName.TryGetValue(suggestion.TagName, out var tag))
                        merged.Add(suggestion with { TagId = tag.Id });
                }
            }
        }

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

            foreach (var videoId in newVideoIds)
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
}
