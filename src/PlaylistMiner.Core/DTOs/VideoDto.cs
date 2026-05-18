using PlaylistMiner.Core.Models;

namespace PlaylistMiner.Core.DTOs;

public record VideoDto(
    int Id,
    string YouTubeId,
    string Title,
    string ChannelName,
    string ThumbnailUrl,
    TimeSpan Duration,
    DateTime PublishedAt,
    VideoStatus Status,
    List<TagSuggestionDto> Tags);
