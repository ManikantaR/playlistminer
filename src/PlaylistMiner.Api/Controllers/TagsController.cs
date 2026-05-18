using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlaylistMiner.Api.Models;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;

namespace PlaylistMiner.Api.Controllers;

[ApiController]
[Route("api/tags")]
public class TagsController(ITagRepository tagRepository) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<List<TagWithCountDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync(CancellationToken ct = default)
    {
        var tags = await tagRepository.GetAllAsync(ct);
        return Ok(tags);
    }

    [HttpPost]
    [ProducesResponseType<TagWithCountDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateTagRequest request, CancellationToken ct = default)
    {
        try
        {
            var tag = await tagRepository.CreateAsync(new Tag
            {
                Name = request.Name,
                Slug = string.Empty,
                Category = request.Category
            }, ct);

            var dto = new TagWithCountDto(tag.Id, tag.Name, tag.Slug, tag.Category, 0);
            return Created($"/api/tags/{tag.Id}", dto);
        }
        catch (DbUpdateException)
        {
            return Conflict(new ProblemDetails { Title = "Tag with this name already exists." });
        }
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct = default)
    {
        await tagRepository.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:int}/rules")]
    [ProducesResponseType<List<TagRule>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRulesAsync(int id, CancellationToken ct = default)
    {
        var rules = await tagRepository.GetRulesAsync(id, ct);
        return Ok(rules);
    }

    [HttpPost("{id:int}/rules")]
    [ProducesResponseType<TagRule>(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddRuleAsync(
        int id,
        [FromBody] AddRuleRequest request,
        CancellationToken ct = default)
    {
        var rule = await tagRepository.AddRuleAsync(new TagRule
        {
            TagId = id,
            Keyword = request.Keyword,
            Field = request.Field,
            Weight = request.Weight
        }, ct);

        return Created($"/api/tags/{id}/rules/{rule.Id}", rule);
    }

    [HttpDelete("{id:int}/rules/{ruleId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteRuleAsync(int id, int ruleId, CancellationToken ct = default)
    {
        await tagRepository.DeleteRuleAsync(ruleId, ct);
        return NoContent();
    }
}
