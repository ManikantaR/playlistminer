namespace PlaylistMiner.Core.DTOs;

public record ImportResult(int VideosFound, int VideosImported, int Skipped, int Errors, string BatchId);
