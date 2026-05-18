# Prompt 03: YouTube API Integration

## Context
PlaylistMiner needs to sync playlists from YouTube, fetch video metadata, and move videos between playlists. We use Google API key for public reads and OAuth 2.0 for private playlist access. TDD with xUnit — write tests first.

## Prompt 03a: YouTube API Client (TDD)

```
In PlaylistMiner.UnitTests, create tests first for a YouTubeApiClient:

YouTubeApiClientTests.cs:
- Test_FetchUserPlaylists_ReturnsMappedPlaylists (mock HTTP response)
- Test_FetchPlaylistItems_PaginatesCorrectly (mock multi-page response)
- Test_FetchVideoMetadata_BatchesBy50 (verify batching logic)
- Test_FetchVideoMetadata_HandlesDeletedVideos (status mapping)
- Test_MoveVideo_AddsToTargetThenRemovesFromSource (verify order)
- Test_QuotaExceeded_ThrowsQuotaExhaustedException (429 handling)
- Test_RateLimiting_RetriesWithBackoff (exponential backoff)

Then implement in PlaylistMiner.Infrastructure/YouTube/:

IYouTubeApiClient interface in Core:
- Task<List<PlaylistDto>> GetUserPlaylistsAsync(CancellationToken ct)
- Task<List<PlaylistItemDto>> GetPlaylistItemsAsync(string playlistId, CancellationToken ct)
- Task<List<VideoMetadataDto>> GetVideoMetadataAsync(IEnumerable<string> videoIds, CancellationToken ct)
- Task AddVideoToPlaylistAsync(string playlistId, string videoId, CancellationToken ct)
- Task RemoveVideoFromPlaylistAsync(string playlistId, string playlistItemId, CancellationToken ct)
- Task<PlaylistDto> CreatePlaylistAsync(string title, string description, CancellationToken ct)

YouTubeApiClient implementation:
- Use HttpClient with named client "YouTube" (base URL: https://www.googleapis.com/youtube/v3)
- API key appended as query param for public endpoints
- OAuth Bearer token from ITokenProvider for private endpoints
- Rate limiting: max 10 requests/second using SemaphoreSlim
- Retry policy: exponential backoff on 429/503 (Polly library)
- Batch video IDs in groups of 50 for videos.list
- Map API responses to DTOs
- Handle pagination via nextPageToken

Use Polly NuGet for retry policies.
Use System.Net.Http.Json for JSON handling.
All methods accept CancellationToken.
```

## Prompt 03b: OAuth Token Management (TDD)

```
TDD — write tests first:

OAuthTokenProviderTests.cs:
- Test_GetAccessToken_ReturnsCachedToken_WhenNotExpired
- Test_GetAccessToken_RefreshesToken_WhenExpired
- Test_GetAccessToken_ThrowsWhenRefreshFails
- Test_InitiateOAuthFlow_ReturnsAuthorizationUrl
- Test_ExchangeCode_StoresRefreshToken

Then implement:

ITokenProvider interface in Core:
- Task<string> GetAccessTokenAsync(CancellationToken ct)
- string GetAuthorizationUrl()
- Task ExchangeCodeAsync(string code, CancellationToken ct)
- Task<bool> IsConnectedAsync(CancellationToken ct)

OAuthTokenProvider implementation in Infrastructure:
- Store refresh token encrypted in PostgreSQL settings table
- Cache access token in memory with expiry tracking
- Use Google OAuth 2.0 desktop flow (redirect to localhost)
- Client ID and Client Secret from configuration
- Encrypt refresh token with AES-256 using a key from configuration
```

## Prompt 03c: Sync Service (TDD)

```
TDD — write tests first:

SyncServiceTests.cs:
- Test_FullSync_FetchesAllPlaylistsAndVideos
- Test_FullSync_UpsertsNewVideos
- Test_FullSync_DetectsDeletedVideos_MarksArchived
- Test_FullSync_SkipsUnchangedVideos
- Test_FullSync_CreatesSyncLogEntry
- Test_FullSync_HandlesQuotaExhaustion_DeferrsRemaining
- Test_InboxSync_OnlySyncsInboxPlaylist

Then implement:

ISyncService interface in Core:
- Task<SyncResult> FullSyncAsync(CancellationToken ct)
- Task<SyncResult> SyncInboxAsync(CancellationToken ct)

SyncService in Infrastructure:
- Orchestrates YouTube API calls
- Diffs remote vs local videos
- Upserts new/updated videos
- Marks missing videos as appropriate status
- Creates SyncLog entries
- Handles quota exhaustion gracefully (logs, defers)

SyncResult DTO: VideosProcessed, VideosCategorized, Errors, DeferredCount
```

## Verification
- All unit tests pass (Red → Green cycle followed)
- Integration test with WireMock simulating YouTube API responses passes
- OAuth flow can be tested manually with a test Google project
