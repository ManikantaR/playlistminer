namespace PlaylistMiner.Core.Interfaces;

using PlaylistMiner.Core.Categorization;
using PlaylistMiner.Core.DTOs;

public interface IPublicAiCategorizer
{
    Task<List<TagSuggestion>> CategorizeAsync(
        VideoContext video,
        IEnumerable<string> availableTags,
        AutomationPolicyDto policy,
        CancellationToken ct = default);
}
