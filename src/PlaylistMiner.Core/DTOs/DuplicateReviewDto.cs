namespace PlaylistMiner.Core.DTOs;

public record DuplicateReviewDto(
    int VideoId,
    string YouTubeId,
    string Title,
    string ThumbnailUrl,
    int PlaylistCount,
    List<DuplicatePlaylistDto> Playlists);
