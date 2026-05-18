using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Models;

namespace PlaylistMiner.Core.Interfaces;

public interface ITagRepository
{
    Task<List<TagWithCountDto>> GetAllAsync(CancellationToken ct = default);
    Task<Tag> CreateAsync(Tag tag, CancellationToken ct = default);
    Task<Tag> UpdateAsync(Tag tag, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<List<TagRule>> GetRulesAsync(int tagId, CancellationToken ct = default);
    Task<TagRule> AddRuleAsync(TagRule rule, CancellationToken ct = default);
    Task DeleteRuleAsync(int ruleId, CancellationToken ct = default);
}
