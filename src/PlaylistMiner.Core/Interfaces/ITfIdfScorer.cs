namespace PlaylistMiner.Core.Interfaces;

using PlaylistMiner.Core.Categorization;

public interface ITfIdfScorer
{
    Task BuildCorpusAsync(CancellationToken ct = default);
    Task<List<TagSuggestion>> ScoreAsync(VideoContext video, CancellationToken ct = default);
}
