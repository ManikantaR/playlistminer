namespace PlaylistMiner.Infrastructure.Categorization;

using Microsoft.Extensions.Options;
using PlaylistMiner.Core.Categorization;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using System.Text.RegularExpressions;

public class KeywordMatcher(ITagRuleRepository ruleRepository, IOptions<CategorizationOptions> options) : IKeywordMatcher
{
    private const float BothFieldDescriptionOnlyMultiplier = 0.5f;

    public async Task<List<TagSuggestion>> MatchAsync(VideoContext video, CancellationToken ct = default)
    {
        var rules = await ruleRepository.GetAllActiveRulesAsync(ct);
        var threshold = options.Value.KeywordThreshold;

        var aggregated = new Dictionary<int, (string TagName, float Weight)>();

        foreach (var rule in rules)
        {
            var matchesTitle = rule.Field is TagRuleField.Title or TagRuleField.Both
                && ContainsKeyword(video.Title, rule.Keyword);
            var matchesDesc = rule.Field is TagRuleField.Description or TagRuleField.Both
                && ContainsKeyword(video.Description, rule.Keyword);

            if (!matchesTitle && !matchesDesc)
                continue;

            var contribution = GetContribution(rule, matchesTitle, matchesDesc);
            if (aggregated.TryGetValue(rule.TagId, out var existing))
                aggregated[rule.TagId] = (existing.TagName, Math.Min(1.0f, existing.Weight + contribution));
            else
                aggregated[rule.TagId] = (rule.Tag.Name, contribution);
        }

        return [..aggregated
            .Where(kv => kv.Value.Weight >= threshold)
            .Select(kv => new TagSuggestion(kv.Key, kv.Value.TagName, kv.Value.Weight, TagSource.RuleBased))];
    }

    private static bool ContainsKeyword(string text, string keyword)
    {
        var normalizedKeyword = keyword.Trim();
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            return false;
        }

        var pattern = $@"(?<![A-Za-z0-9]){Regex.Escape(normalizedKeyword)}(?![A-Za-z0-9])";
        return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static float GetContribution(TagRule rule, bool matchesTitle, bool matchesDesc)
    {
        if (rule.Field is TagRuleField.Both && !matchesTitle && matchesDesc)
        {
            return rule.Weight * BothFieldDescriptionOnlyMultiplier;
        }

        return rule.Weight;
    }
}
