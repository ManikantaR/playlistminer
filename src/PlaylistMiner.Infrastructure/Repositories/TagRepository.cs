using Microsoft.EntityFrameworkCore;
using PlaylistMiner.Core;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.Infrastructure.Repositories;

public class TagRepository(PlaylistMinerDbContext db) : ITagRepository
{
    public async Task<List<TagWithCountDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.Tags
            .AsNoTracking()
            .Select(t => new TagWithCountDto(
                t.Id,
                t.Name,
                t.Slug,
                t.Category,
                t.VideoTags.Count))
            .OrderBy(t => t.Name)
            .ToListAsync(ct);
    }

    public async Task<Tag> CreateAsync(Tag tag, CancellationToken ct = default)
    {
        tag.Slug = SlugGenerator.Generate(tag.Name);
        tag.CreatedAt = DateTime.UtcNow;
        db.Tags.Add(tag);
        await db.SaveChangesAsync(ct);
        return tag;
    }

    public async Task<Tag> UpdateAsync(Tag tag, CancellationToken ct = default)
    {
        tag.Slug = SlugGenerator.Generate(tag.Name);
        db.Tags.Update(tag);
        await db.SaveChangesAsync(ct);
        return tag;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await db.VideoTags.Where(vt => vt.TagId == id).ExecuteDeleteAsync(ct);
        await db.Tags.Where(t => t.Id == id).ExecuteDeleteAsync(ct);
    }

    public async Task<List<TagRule>> GetRulesAsync(int tagId, CancellationToken ct = default)
    {
        return await db.TagRules
            .AsNoTracking()
            .Where(r => r.TagId == tagId)
            .ToListAsync(ct);
    }

    public async Task<TagRule> AddRuleAsync(TagRule rule, CancellationToken ct = default)
    {
        rule.CreatedAt = DateTime.UtcNow;
        rule.UpdatedAt = DateTime.UtcNow;
        db.TagRules.Add(rule);
        await db.SaveChangesAsync(ct);
        return rule;
    }

    public async Task DeleteRuleAsync(int ruleId, CancellationToken ct = default)
    {
        await db.TagRules.Where(r => r.Id == ruleId).ExecuteDeleteAsync(ct);
    }
}
