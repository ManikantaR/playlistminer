namespace PlaylistMiner.Core.DTOs;

public record UndoLogDto(
    int Id,
    int VideoId,
    string VideoTitle,
    int SourcePlaylistId,
    string SourcePlaylistName,
    int TargetPlaylistId,
    string TargetPlaylistName,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    bool IsUndone);
