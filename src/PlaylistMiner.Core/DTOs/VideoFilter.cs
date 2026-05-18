using PlaylistMiner.Core.Models;

namespace PlaylistMiner.Core.DTOs;

public record VideoFilter(
    string? Search = null,
    int[]? Tags = null,
    VideoStatus? Status = null,
    int? PlaylistId = null,
    int Page = 1,
    int PageSize = 20);
