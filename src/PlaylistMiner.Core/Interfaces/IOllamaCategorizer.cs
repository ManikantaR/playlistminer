namespace PlaylistMiner.Core.Interfaces;

using PlaylistMiner.Core.Categorization;

public interface IOllamaCategorizer
{
    Task<List<TagSuggestion>> CategorizeAsync(VideoContext video, IEnumerable<string> availableTags, CancellationToken ct = default);
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}
