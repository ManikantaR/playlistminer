namespace PlaylistMiner.Core.Interfaces;

public interface IVideoService
{
    Task AcceptTagAsync(int videoId, int tagId, CancellationToken ct = default);
    Task RejectTagAsync(int videoId, int tagId, CancellationToken ct = default);
    Task AddTagAsync(int videoId, int tagId, CancellationToken ct = default);
    Task RemoveTagAsync(int videoId, int tagId, CancellationToken ct = default);
}
