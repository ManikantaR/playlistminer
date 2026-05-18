using PlaylistMiner.Core.Models;

namespace PlaylistMiner.Core.DTOs;

public record VideoMetadataDto(
    string YouTubeId,
    string Title,
    string Description,
    string ChannelName,
    string ChannelId,
    string ThumbnailUrl,
    TimeSpan Duration,
    DateTime PublishedAt,
    VideoStatus Status);
