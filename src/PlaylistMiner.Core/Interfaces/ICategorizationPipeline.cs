namespace PlaylistMiner.Core.Interfaces;

using PlaylistMiner.Core.Categorization;

public interface ICategorizationPipeline
{
    Task<List<TagSuggestion>> CategorizeAsync(int videoId, CancellationToken ct = default);
    Task CategorizeNewVideosAsync(CancellationToken ct = default);
}
