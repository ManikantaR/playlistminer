namespace PlaylistMiner.Infrastructure.Categorization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlaylistMiner.Core.Categorization;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;

public class SelfLearningService(
    PlaylistMinerDbContext db,
    ITfIdfScorer tfIdfScorer,
    ILogger<SelfLearningService> logger) : ISelfLearningService
{
    private const float IncrementStep = 0.1f;
    private const float NewRuleWeight = 0.3f;

    public async Task OnTagAcceptedAsync(int videoId, int tagId, CancellationToken ct = default)
    {
        var video = await db.Videos.FindAsync([videoId], ct);
        if (video is null)
        {
            logger.LogWarning("Video {VideoId} not found for self-learning", videoId);
            return;
        }

        var words = ExtractSignificantWords(video.Title, video.Description);
        var existingRules = await db.TagRules
            .Where(r => r.TagId == tagId)
            .ToListAsync(ct);

        foreach (var word in words)
        {
            var existing = existingRules.FirstOrDefault(
                r => string.Equals(r.Keyword, word, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                db.TagRules.Add(new TagRule
                {
                    TagId = tagId,
                    Keyword = word,
                    Field = TagRuleField.Both,
                    Weight = NewRuleWeight,
                    IsLearned = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else if (existing.IsLearned)
            {
                existing.Weight = Math.Min(1.0f, existing.Weight + IncrementStep);
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
        await tfIdfScorer.BuildCorpusAsync(ct);

        logger.LogInformation("OnTagAccepted: video={VideoId} tag={TagId} words={Count}", videoId, tagId, words.Count);
    }

    public async Task OnTagRejectedAsync(int videoId, int tagId, CancellationToken ct = default)
    {
        var video = await db.Videos.FindAsync([videoId], ct);
        if (video is null)
        {
            logger.LogWarning("Video {VideoId} not found for self-learning rejection", videoId);
            return;
        }

        var words = ExtractSignificantWords(video.Title, video.Description);
        var existingRules = await db.TagRules
            .Where(r => r.TagId == tagId && r.IsLearned)
            .ToListAsync(ct);

        var toRemove = new List<TagRule>();
        foreach (var word in words)
        {
            var rule = existingRules.FirstOrDefault(
                r => string.Equals(r.Keyword, word, StringComparison.OrdinalIgnoreCase));

            if (rule is null) continue;

            rule.Weight -= IncrementStep;
            if (rule.Weight <= 0f)
                toRemove.Add(rule);
            else
                rule.UpdatedAt = DateTime.UtcNow;
        }

        if (toRemove.Count > 0)
            db.TagRules.RemoveRange(toRemove);

        await db.SaveChangesAsync(ct);
        await tfIdfScorer.BuildCorpusAsync(ct);

        logger.LogInformation("OnTagRejected: video={VideoId} tag={TagId}", videoId, tagId);
    }

    private static List<string> ExtractSignificantWords(string title, string description)
        => StopWords.Tokenize($"{title} {description}").Distinct().ToList();
}
