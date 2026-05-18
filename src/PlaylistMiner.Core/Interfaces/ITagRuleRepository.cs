namespace PlaylistMiner.Core.Interfaces;

using PlaylistMiner.Core.Models;

public interface ITagRuleRepository
{
    Task<List<TagRule>> GetAllActiveRulesAsync(CancellationToken ct = default);
}
