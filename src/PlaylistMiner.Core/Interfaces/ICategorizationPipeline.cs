namespace PlaylistMiner.Core.Interfaces;

using PlaylistMiner.Core.Categorization;

public interface ICategorizationPipeline
{
    Task<List<TagSuggestion>> ClassifyAsync(int videoId, CancellationToken ct = default);
    Task<List<TagSuggestion>> CategorizeAsync(int videoId, string? runId = null, CancellationToken ct = default);
    Task CategorizeNewVideosAsync(CancellationToken ct = default);
    Task ReclassifyGeneratedAsync(CancellationToken ct = default);
}
