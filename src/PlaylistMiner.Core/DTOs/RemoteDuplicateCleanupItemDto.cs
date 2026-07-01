namespace PlaylistMiner.Core.DTOs;

public record RemoteDuplicateCleanupItemDto(
    int VideoId,
    string YouTubeId,
    string Title,
    int WinnerPlaylistId,
    string WinnerPlaylistName,
    bool HasUnresolvedRemovals,
    List<RemoteDuplicateRemovalTargetDto> LoserPlaylists);
