using Microsoft.AspNetCore.Mvc;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Api.Models;

namespace PlaylistMiner.Api.Controllers;

[ApiController]
[Route("api/videos")]
public class VideosController(IVideoRepository videoRepository, IVideoService videoService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<VideoDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync(
        [FromQuery] string? search,
        [FromQuery] string? tags,
        [FromQuery] VideoStatus? status,
        [FromQuery] int? playlistId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        int[]? tagIds = null;
        if (!string.IsNullOrEmpty(tags))
        {
            tagIds = tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => int.TryParse(t.Trim(), out var id) ? id : -1)
                .Where(id => id > 0)
                .ToArray();
        }

        var filter = new VideoFilter(search, tagIds, status, playlistId, page, pageSize);
        var result = await videoRepository.GetAllAsync(filter, ct);
        return Ok(result);
    }

    [HttpGet("suggestions")]
    [ProducesResponseType<PagedResult<VideoDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSuggestionsAsync(CancellationToken ct = default)
    {
        var filter = new VideoFilter(Tags: null, Status: VideoStatus.Active);
        var result = await videoRepository.GetAllAsync(filter, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<VideoDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var video = await videoRepository.GetByIdAsync(id, ct);
        return video is null ? NotFound() : Ok(video);
    }

    [HttpPatch("{id:int}/tags")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchTagsAsync(
        int id,
        [FromBody] PatchTagsRequest request,
        CancellationToken ct = default)
    {
        foreach (var tagId in request.TagIdsToAdd)
            await videoService.AddTagAsync(id, tagId, ct);

        foreach (var tagId in request.TagIdsToRemove)
            await videoService.RemoveTagAsync(id, tagId, ct);

        return Ok();
    }

    [HttpPost("{id:int}/suggestions/{tagId:int}/accept")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AcceptAsync(int id, int tagId, CancellationToken ct = default)
    {
        await videoService.AcceptTagAsync(id, tagId, ct);
        return Ok();
    }

    [HttpPost("{id:int}/suggestions/{tagId:int}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RejectAsync(int id, int tagId, CancellationToken ct = default)
    {
        await videoService.RejectTagAsync(id, tagId, ct);
        return Ok();
    }
}
