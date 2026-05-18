namespace PlaylistMiner.Core.Interfaces;

using PlaylistMiner.Core.Categorization;

public interface IKeywordMatcher
{
    Task<List<TagSuggestion>> MatchAsync(VideoContext video, CancellationToken ct = default);
}
