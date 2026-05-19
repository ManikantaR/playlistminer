using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Exceptions;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using Polly;

namespace PlaylistMiner.Infrastructure.YouTube;

public sealed class YouTubeApiClient : IYouTubeApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ITokenProvider _tokenProvider;
    private readonly IQuotaTracker? _quotaTracker;
    private readonly ResiliencePipeline<HttpResponseMessage> _retryPipeline;
    private readonly SemaphoreSlim _rateLimiter = new(10, 10);

    public YouTubeApiClient(
        HttpClient httpClient,
        ITokenProvider tokenProvider,
        IQuotaTracker? quotaTracker = null,
        ResiliencePipeline<HttpResponseMessage>? retryPipeline = null)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _quotaTracker = quotaTracker;
        _retryPipeline = retryPipeline ?? BuildDefaultRetryPipeline();
    }

    private static ResiliencePipeline<HttpResponseMessage> BuildDefaultRetryPipeline()
    {
        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new Polly.Retry.RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests
                                    || r.StatusCode == HttpStatusCode.ServiceUnavailable)
            })
            .Build();
    }

    // ─── Public API ──────────────────────────────────────────────────────────────

    public async Task<List<PlaylistDto>> GetUserPlaylistsAsync(CancellationToken ct = default)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(ct);
        var results = new List<PlaylistDto>();
        string? pageToken = null;

        do
        {
            var url = BuildUrl("playlists", [
                ("part", "snippet,contentDetails"),
                ("mine", "true"),
                ("maxResults", "50"),
                .. (pageToken is not null ? new[] { ("pageToken", pageToken) } : Array.Empty<(string, string)>())
            ]);

            var response = await ExecuteWithRateLimitAsync(() =>
                SendWithTokenAsync(HttpMethod.Get, url, token, null, ct), ct);

            await EnsureSuccessOrThrowAsync(response, ct);

            var page = await response.Content.ReadFromJsonAsync<YouTubeListResponse<YouTubePlaylist>>(JsonOptions, ct)
                ?? throw new InvalidOperationException("Null response from playlists endpoint.");

            results.AddRange(page.Items.Select(MapPlaylist));
            pageToken = page.NextPageToken;
        } while (pageToken is not null);

        return results;
    }

    public async Task<List<PlaylistItemDto>> GetPlaylistItemsAsync(string playlistId, CancellationToken ct = default)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(ct);
        var results = new List<PlaylistItemDto>();
        string? pageToken = null;

        do
        {
            var url = BuildUrl("playlistItems", [
                ("part", "snippet"),
                ("playlistId", playlistId),
                ("maxResults", "50"),
                .. (pageToken is not null ? new[] { ("pageToken", pageToken) } : Array.Empty<(string, string)>())
            ]);

            var response = await ExecuteWithRateLimitAsync(() =>
                SendWithTokenAsync(HttpMethod.Get, url, token, null, ct), ct);

            await EnsureSuccessOrThrowAsync(response, ct);

            var page = await response.Content.ReadFromJsonAsync<YouTubeListResponse<YouTubePlaylistItem>>(JsonOptions, ct)
                ?? throw new InvalidOperationException("Null response from playlistItems endpoint.");

            results.AddRange(page.Items.Select(MapPlaylistItem));
            pageToken = page.NextPageToken;
        } while (pageToken is not null);

        return results;
    }

    public async Task<List<VideoMetadataDto>> GetVideoMetadataAsync(
        IEnumerable<string> videoIds, CancellationToken ct = default)
    {
        var idList = videoIds.ToList();
        var results = new List<VideoMetadataDto>();

        foreach (var batch in idList.Chunk(50))
        {
            var ids = string.Join(",", batch);
            var url = BuildUrl("videos", [
                ("part", "snippet,contentDetails,status"),
                ("id", ids)
            ]);

            var response = await ExecuteWithRateLimitAsync(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                return _httpClient.SendAsync(request, ct);
            }, ct);

            await EnsureSuccessOrThrowAsync(response, ct);

            var page = await response.Content.ReadFromJsonAsync<YouTubeListResponse<YouTubeVideo>>(JsonOptions, ct)
                ?? throw new InvalidOperationException("Null response from videos endpoint.");

            results.AddRange(page.Items.Select(MapVideo));
        }

        return results;
    }

    public async Task AddVideoToPlaylistAsync(string playlistId, string videoId, CancellationToken ct = default)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(ct);
        var body = JsonSerializer.Serialize(new
        {
            snippet = new
            {
                playlistId,
                resourceId = new { kind = "youtube#video", videoId }
            }
        });

        var response = await ExecuteWithRateLimitAsync(() =>
            SendWithTokenAsync(HttpMethod.Post, "playlistItems?part=snippet",
                token, new StringContent(body, Encoding.UTF8, "application/json"), ct), ct);

        await EnsureSuccessOrThrowAsync(response, ct);
    }

    public async Task RemoveVideoFromPlaylistAsync(string playlistId, string playlistItemId, CancellationToken ct = default)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(ct);
        var url = $"playlistItems?id={Uri.EscapeDataString(playlistItemId)}";

        var response = await ExecuteWithRateLimitAsync(() =>
            SendWithTokenAsync(HttpMethod.Delete, url, token, null, ct), ct);

        if (response.StatusCode != HttpStatusCode.NoContent)
            await EnsureSuccessOrThrowAsync(response, ct);
    }

    public async Task<PlaylistDto> CreatePlaylistAsync(string title, string description, CancellationToken ct = default)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(ct);
        var body = JsonSerializer.Serialize(new
        {
            snippet = new { title, description },
            status = new { privacyStatus = "private" }
        });

        var response = await ExecuteWithRateLimitAsync(() =>
            SendWithTokenAsync(HttpMethod.Post, "playlists?part=snippet,contentDetails",
                token, new StringContent(body, Encoding.UTF8, "application/json"), ct), ct);

        await EnsureSuccessOrThrowAsync(response, ct);

        var playlist = await response.Content.ReadFromJsonAsync<YouTubePlaylist>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Null response from createPlaylist endpoint.");

        return MapPlaylist(playlist);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> ExecuteWithRateLimitAsync(
        Func<Task<HttpResponseMessage>> action, CancellationToken ct)
    {
        if (_quotaTracker is not null && await _quotaTracker.IsQuotaExhaustedAsync(ct))
            throw new QuotaExhaustedException();

        await _rateLimiter.WaitAsync(ct);
        try
        {
            return await _retryPipeline.ExecuteAsync(
                async token => await action(),
                ct);
        }
        finally
        {
            // Release after 1 second without blocking the caller
            _ = Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None)
                .ContinueWith(_ => _rateLimiter.Release(), CancellationToken.None);
        }
    }

    private Task<HttpResponseMessage> SendWithTokenAsync(
        HttpMethod method, string url, string token, HttpContent? content, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        if (content is not null)
            request.Content = content;
        return _httpClient.SendAsync(request, ct);
    }

    private async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (body.Contains("quotaExceeded", StringComparison.OrdinalIgnoreCase))
            {
                if (_quotaTracker is not null)
                    await _quotaTracker.RecordQuotaExhaustedAsync(ct);
                throw new QuotaExhaustedException();
            }
        }

        response.EnsureSuccessStatusCode();
    }

    private static string BuildUrl(string endpoint, (string Key, string Value)[] queryParams)
    {
        var sb = new StringBuilder(endpoint);
        sb.Append('?');
        for (var i = 0; i < queryParams.Length; i++)
        {
            if (i > 0) sb.Append('&');
            sb.Append(Uri.EscapeDataString(queryParams[i].Key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(queryParams[i].Value));
        }
        return sb.ToString();
    }

    // ─── Mapping ──────────────────────────────────────────────────────────────────

    private static PlaylistDto MapPlaylist(YouTubePlaylist p) =>
        new(
            p.Id,
            p.Snippet.Title,
            p.Snippet.Description,
            IsInbox: false,
            ItemCount: int.TryParse(p.ContentDetails?.ItemCount, out var n) ? n : 0);

    private static PlaylistItemDto MapPlaylistItem(YouTubePlaylistItem i) =>
        new(
            i.Id,
            i.Snippet.ResourceId?.VideoId ?? string.Empty,
            i.Snippet.Position,
            i.Snippet.VideoPublishedAt?.UtcDateTime ?? DateTime.UtcNow);

    private static VideoMetadataDto MapVideo(YouTubeVideo v)
    {
        var privacy = v.Status?.PrivacyStatus ?? "public";
        var status = privacy is "private" or "unlisted"
            ? VideoStatus.Private
            : VideoStatus.Active;

        var duration = TimeSpan.Zero;
        if (!string.IsNullOrEmpty(v.ContentDetails?.Duration))
        {
            try { duration = XmlConvert.ToTimeSpan(v.ContentDetails.Duration); }
            catch { /* ignore invalid durations */ }
        }

        return new VideoMetadataDto(
            v.Id,
            v.Snippet.Title,
            v.Snippet.Description ?? string.Empty,
            v.Snippet.ChannelTitle ?? string.Empty,
            v.Snippet.ChannelId ?? string.Empty,
            v.Snippet.Thumbnails?.High?.Url ?? string.Empty,
            duration,
            v.Snippet.PublishedAt?.UtcDateTime ?? DateTime.UtcNow,
            status);
    }

    // ─── Internal response types ──────────────────────────────────────────────────

    private sealed record YouTubeListResponse<T>
    {
        [JsonPropertyName("nextPageToken")]
        public string? NextPageToken { get; init; }

        [JsonPropertyName("items")]
        public List<T> Items { get; init; } = [];
    }

    private sealed record YouTubePlaylist
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("snippet")]
        public YouTubeSnippet Snippet { get; init; } = new();

        [JsonPropertyName("contentDetails")]
        public YouTubeContentDetails? ContentDetails { get; init; }
    }

    private sealed record YouTubePlaylistItem
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("snippet")]
        public YouTubePlaylistItemSnippet Snippet { get; init; } = new();
    }

    private sealed record YouTubeVideo
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("snippet")]
        public YouTubeVideoSnippet Snippet { get; init; } = new();

        [JsonPropertyName("contentDetails")]
        public YouTubeVideoContentDetails? ContentDetails { get; init; }

        [JsonPropertyName("status")]
        public YouTubeVideoStatus? Status { get; init; }
    }

    private record YouTubeSnippet
    {
        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("thumbnails")]
        public YouTubeThumbnails? Thumbnails { get; init; }

        [JsonPropertyName("channelTitle")]
        public string? ChannelTitle { get; init; }

        [JsonPropertyName("channelId")]
        public string? ChannelId { get; init; }

        [JsonPropertyName("publishedAt")]
        public DateTimeOffset? PublishedAt { get; init; }
    }

    private sealed record YouTubeVideoSnippet : YouTubeSnippet;

    private sealed record YouTubePlaylistItemSnippet
    {
        [JsonPropertyName("playlistId")]
        public string PlaylistId { get; init; } = string.Empty;

        [JsonPropertyName("resourceId")]
        public YouTubeResourceId? ResourceId { get; init; }

        [JsonPropertyName("position")]
        public int Position { get; init; }

        [JsonPropertyName("videoPublishedAt")]
        public DateTimeOffset? VideoPublishedAt { get; init; }
    }

    private sealed record YouTubeResourceId
    {
        [JsonPropertyName("videoId")]
        public string? VideoId { get; init; }
    }

    private sealed record YouTubeVideoStatus
    {
        [JsonPropertyName("privacyStatus")]
        public string PrivacyStatus { get; init; } = "public";
    }

    private sealed record YouTubeThumbnails
    {
        [JsonPropertyName("high")]
        public YouTubeThumbnail? High { get; init; }
    }

    private sealed record YouTubeThumbnail
    {
        [JsonPropertyName("url")]
        public string Url { get; init; } = string.Empty;
    }

    private sealed record YouTubeContentDetails
    {
        [JsonPropertyName("itemCount")]
        public string? ItemCount { get; init; }

        [JsonPropertyName("duration")]
        public string? Duration { get; init; }
    }

    private sealed record YouTubeVideoContentDetails
    {
        [JsonPropertyName("duration")]
        public string Duration { get; init; } = string.Empty;
    }
}
