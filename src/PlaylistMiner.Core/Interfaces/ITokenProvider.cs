namespace PlaylistMiner.Core.Interfaces;

public interface ITokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken ct = default);
    string GetAuthorizationUrl();
    Task ExchangeCodeAsync(string code, CancellationToken ct = default);
    Task<bool> IsConnectedAsync(CancellationToken ct = default);
}
