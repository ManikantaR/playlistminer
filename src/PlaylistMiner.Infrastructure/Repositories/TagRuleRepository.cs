namespace PlaylistMiner.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;

public class TagRuleRepository(PlaylistMinerDbContext db) : ITagRuleRepository
{
    public Task<List<TagRule>> GetAllActiveRulesAsync(CancellationToken ct = default)
        => db.TagRules.Include(r => r.Tag).ToListAsync(ct);
}
