namespace PlaylistMiner.Core.DTOs;

public record RemoteDuplicateRemovalTargetDto(
    int PlaylistId,
    string PlaylistName,
    string? PlaylistItemId);
