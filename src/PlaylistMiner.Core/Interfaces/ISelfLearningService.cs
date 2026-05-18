namespace PlaylistMiner.Core.Interfaces;

public interface ISelfLearningService
{
    Task OnTagAcceptedAsync(int videoId, int tagId, CancellationToken ct = default);
    Task OnTagRejectedAsync(int videoId, int tagId, CancellationToken ct = default);
}
