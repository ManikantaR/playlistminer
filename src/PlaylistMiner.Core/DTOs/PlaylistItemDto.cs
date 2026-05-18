namespace PlaylistMiner.Core.DTOs;

public record PlaylistItemDto(
    string PlaylistItemId,
    string VideoId,
    int Position,
    DateTime AddedAt);
