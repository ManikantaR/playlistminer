namespace PlaylistMiner.Api.Models;

public record RestorePlaylistSampleRequest(int SourcePlaylistId, int MaxCount = 5);
