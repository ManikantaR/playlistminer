namespace PlaylistMiner.Core.DTOs;

public record DuplicatePlaylistDto(
    int PlaylistId,
    string PlaylistName,
    bool IsManaged,
    string? Topic);
