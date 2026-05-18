namespace PlaylistMiner.Infrastructure.YouTube;

public class YouTubeSettings
{
    public const string SectionName = "YouTube";
    public string ApiKey { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string EncryptionKey { get; set; } = string.Empty; // Base64-encoded 32 bytes
    public string RedirectUri { get; set; } = "http://localhost:8888/oauth/callback";
}
