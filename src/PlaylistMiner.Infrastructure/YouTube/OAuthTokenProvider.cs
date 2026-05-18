using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.Infrastructure.YouTube;

public sealed class OAuthTokenProvider : ITokenProvider
{
    private const string RefreshTokenKey = "oauth.refresh_token";
    private const string GoogleTokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string GoogleAuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string Scopes = "https://www.googleapis.com/auth/youtube https://www.googleapis.com/auth/youtube.force-ssl";
    private static readonly TimeSpan TokenExpiryBuffer = TimeSpan.FromMinutes(5);

    private readonly YouTubeSettings _settings;
    private readonly PlaylistMinerDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;

    private string? _cachedToken;
    private DateTime _tokenExpiry;

    public OAuthTokenProvider(
        IOptions<YouTubeSettings> settings,
        PlaylistMinerDbContext db,
        IHttpClientFactory httpClientFactory)
    {
        _settings = settings.Value;
        _db = db;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiry - TokenExpiryBuffer)
            return _cachedToken;

        // Load and decrypt refresh token from DB
        var setting = await _db.Settings.FindAsync([RefreshTokenKey], ct);
        if (setting is null)
            throw new InvalidOperationException("Not connected to YouTube. Call ExchangeCodeAsync first.");

        var refreshToken = Decrypt(setting.Value);
        return await RefreshAccessTokenAsync(refreshToken, ct);
    }

    public string GetAuthorizationUrl()
    {
        var query = new StringBuilder("?");
        query.Append($"client_id={Uri.EscapeDataString(_settings.ClientId)}&");
        query.Append($"redirect_uri={Uri.EscapeDataString(_settings.RedirectUri)}&");
        query.Append($"response_type=code&");
        query.Append($"scope={Uri.EscapeDataString(Scopes)}&");
        query.Append("access_type=offline&");
        query.Append("prompt=consent");
        return GoogleAuthEndpoint + query;
    }

    public async Task ExchangeCodeAsync(string code, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("GoogleOAuth");
        var content = new FormUrlEncodedContent([
            new("code", code),
            new("client_id", _settings.ClientId),
            new("client_secret", _settings.ClientSecret),
            new("redirect_uri", _settings.RedirectUri),
            new("grant_type", "authorization_code")
        ]);

        var response = await client.PostAsync(GoogleTokenEndpoint, content, ct);
        response.EnsureSuccessStatusCode();

        var tokenResponse = await response.Content.ReadFromJsonAsync<OAuthTokenResponse>(ct)
            ?? throw new InvalidOperationException("Null token response from Google.");

        CacheToken(tokenResponse);

        // Store encrypted refresh token
        if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
        {
            await StoreRefreshTokenAsync(tokenResponse.RefreshToken, ct);
        }
    }

    public async Task<bool> IsConnectedAsync(CancellationToken ct = default)
    {
        return await _db.Settings.AnyAsync(s => s.Key == RefreshTokenKey, ct);
    }

    // ─── Private helpers ─────────────────────────────────────────────────────────

    private async Task<string> RefreshAccessTokenAsync(string refreshToken, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("GoogleOAuth");
        var content = new FormUrlEncodedContent([
            new("refresh_token", refreshToken),
            new("client_id", _settings.ClientId),
            new("client_secret", _settings.ClientSecret),
            new("grant_type", "refresh_token")
        ]);

        var response = await client.PostAsync(GoogleTokenEndpoint, content, ct);
        response.EnsureSuccessStatusCode();

        var tokenResponse = await response.Content.ReadFromJsonAsync<OAuthTokenResponse>(ct)
            ?? throw new InvalidOperationException("Null token response from Google refresh.");

        CacheToken(tokenResponse);
        return _cachedToken!;
    }

    private void CacheToken(OAuthTokenResponse tokenResponse)
    {
        _cachedToken = tokenResponse.AccessToken;
        _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
    }

    private async Task StoreRefreshTokenAsync(string refreshToken, CancellationToken ct)
    {
        var encrypted = Encrypt(refreshToken);
        var existing = await _db.Settings.FindAsync([RefreshTokenKey], ct);
        if (existing is null)
        {
            _db.Settings.Add(new Setting
            {
                Key = RefreshTokenKey,
                Value = encrypted,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Value = encrypted;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }

    // ─── AES-256 encryption ───────────────────────────────────────────────────────

    private string Encrypt(string plaintext)
    {
        var key = Convert.FromBase64String(_settings.EncryptionKey);
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertextBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        // Prepend IV to ciphertext, then Base64 encode
        var result = new byte[aes.IV.Length + ciphertextBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(ciphertextBytes, 0, result, aes.IV.Length, ciphertextBytes.Length);
        return Convert.ToBase64String(result);
    }

    private string Decrypt(string ciphertext)
    {
        var key = Convert.FromBase64String(_settings.EncryptionKey);
        var allBytes = Convert.FromBase64String(ciphertext);

        using var aes = Aes.Create();
        aes.Key = key;

        var iv = new byte[aes.BlockSize / 8];
        var ciphertextBytes = new byte[allBytes.Length - iv.Length];
        Buffer.BlockCopy(allBytes, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(allBytes, iv.Length, ciphertextBytes, 0, ciphertextBytes.Length);

        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        var plaintextBytes = decryptor.TransformFinalBlock(ciphertextBytes, 0, ciphertextBytes.Length);
        return Encoding.UTF8.GetString(plaintextBytes);
    }

    // ─── Response DTOs ────────────────────────────────────────────────────────────

    private sealed record OAuthTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; init; } = string.Empty;
    }
}
