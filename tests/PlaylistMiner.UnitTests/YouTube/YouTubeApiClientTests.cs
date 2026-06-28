using System.Net;
using System.Text;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Polly;
using PlaylistMiner.Core.Exceptions;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.YouTube;

namespace PlaylistMiner.UnitTests.YouTube;

[Trait("Category", "Unit")]
public class YouTubeApiClientTests
{
    private static Mock<HttpMessageHandler> CreateHandlerMock() => new(MockBehavior.Strict);

    private static HttpClient CreateHttpClient(Mock<HttpMessageHandler> handlerMock)
    {
        var client = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://www.googleapis.com/youtube/v3/")
        };
        return client;
    }

    private static Mock<ITokenProvider> CreateTokenProviderMock()
    {
        var mock = new Mock<ITokenProvider>();
        mock.Setup(p => p.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-token");
        return mock;
    }

    // Zero-delay retry pipeline: 3 retries on 429/503, no actual delay for fast tests
    private static ResiliencePipeline<HttpResponseMessage> CreateZeroDelayRetryPipeline()
    {
        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new Polly.Retry.RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.Zero,
                BackoffType = DelayBackoffType.Constant,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests
                                    || r.StatusCode == HttpStatusCode.ServiceUnavailable)
            })
            .Build();
    }

    private static void SetupHandlerResponse(
        Mock<HttpMessageHandler> handlerMock,
        HttpStatusCode statusCode,
        string json,
        Func<HttpRequestMessage, bool>? requestMatcher = null)
    {
        var setup = handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                requestMatcher is not null
                    ? ItExpr.Is<HttpRequestMessage>(r => requestMatcher(r))
                    : ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }

    [Fact]
    public async Task Test_GetUserPlaylists_ReturnsMappedPlaylists()
    {
        // Arrange
        const string json = """
            {
                "items": [
                    {
                        "id": "PLtest123",
                        "snippet": {
                            "title": "My Playlist",
                            "description": "A test playlist",
                            "channelTitle": "Test Channel",
                            "channelId": "UC123"
                        },
                        "contentDetails": {
                            "itemCount": "5"
                        }
                    }
                ]
            }
            """;

        var handlerMock = CreateHandlerMock();
        SetupHandlerResponse(handlerMock, HttpStatusCode.OK, json);

        var client = new YouTubeApiClient(
            CreateHttpClient(handlerMock),
            CreateTokenProviderMock().Object,
            quotaTracker: null,
            ResiliencePipeline<HttpResponseMessage>.Empty);

        // Act
        var result = await client.GetUserPlaylistsAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].YouTubeId.Should().Be("PLtest123");
        result[0].Name.Should().Be("My Playlist");
        result[0].Description.Should().Be("A test playlist");
        result[0].ItemCount.Should().Be(5);
    }

    [Fact]
    public async Task Test_GetUserPlaylists_HandlesNumericItemCount()
    {
        // Arrange
        const string json = """
            {
                "items": [
                    {
                        "id": "PLnumeric",
                        "snippet": {
                            "title": "Numeric Playlist",
                            "description": "A live-shaped playlist response"
                        },
                        "contentDetails": {
                            "itemCount": 12
                        }
                    }
                ]
            }
            """;

        var handlerMock = CreateHandlerMock();
        SetupHandlerResponse(handlerMock, HttpStatusCode.OK, json);

        var client = new YouTubeApiClient(
            CreateHttpClient(handlerMock),
            CreateTokenProviderMock().Object,
            quotaTracker: null,
            ResiliencePipeline<HttpResponseMessage>.Empty);

        // Act
        var result = await client.GetUserPlaylistsAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].YouTubeId.Should().Be("PLnumeric");
        result[0].ItemCount.Should().Be(12);
    }

    [Fact]
    public async Task Test_GetPlaylistItems_PaginatesCorrectly()
    {
        // Arrange
        const string page1Json = """
            {
                "nextPageToken": "page2token",
                "items": [
                    {
                        "id": "item1",
                        "snippet": {
                            "playlistId": "PLtest123",
                            "resourceId": { "videoId": "vid1" },
                            "position": 0,
                            "videoPublishedAt": "2024-01-01T00:00:00Z"
                        }
                    }
                ]
            }
            """;

        const string page2Json = """
            {
                "items": [
                    {
                        "id": "item2",
                        "snippet": {
                            "playlistId": "PLtest123",
                            "resourceId": { "videoId": "vid2" },
                            "position": 1,
                            "videoPublishedAt": "2024-01-02T00:00:00Z"
                        }
                    }
                ]
            }
            """;

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var callCount = 0;
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                var json = callCount == 1 ? page1Json : page2Json;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            });

        var client = new YouTubeApiClient(
            CreateHttpClient(handlerMock),
            CreateTokenProviderMock().Object,
            quotaTracker: null,
            ResiliencePipeline<HttpResponseMessage>.Empty);

        // Act
        var result = await client.GetPlaylistItemsAsync("PLtest123");

        // Assert
        result.Should().HaveCount(2);
        result[0].PlaylistItemId.Should().Be("item1");
        result[1].PlaylistItemId.Should().Be("item2");
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task Test_GetVideoMetadata_BatchesBy50()
    {
        // Arrange - 55 video IDs should trigger 2 HTTP calls (50 + 5)
        var videoIds = Enumerable.Range(1, 55).Select(i => $"vid{i:D3}").ToList();

        var callCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r =>
                    r.Headers.Authorization != null &&
                    r.Headers.Authorization.Scheme == "Bearer" &&
                    r.Headers.Authorization.Parameter == "test-token"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"items":[]}""", Encoding.UTF8, "application/json")
                };
            });

        var client = new YouTubeApiClient(
            CreateHttpClient(handlerMock),
            CreateTokenProviderMock().Object,
            quotaTracker: null,
            ResiliencePipeline<HttpResponseMessage>.Empty);

        // Act
        await client.GetVideoMetadataAsync(videoIds);

        // Assert
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task Test_GetVideoMetadata_HandlesDeletedVideos()
    {
        // Arrange - video with privacyStatus=private → VideoStatus.Private
        const string json = """
            {
                "items": [
                    {
                        "id": "privVid1",
                        "snippet": {
                            "title": "Private Video",
                            "description": "",
                            "channelTitle": "Channel",
                            "channelId": "UC123",
                            "publishedAt": "2024-01-01T00:00:00Z",
                            "thumbnails": { "high": { "url": "https://thumb.jpg" } }
                        },
                        "contentDetails": {
                            "duration": "PT5M"
                        },
                        "status": {
                            "privacyStatus": "private"
                        }
                    }
                ]
            }
            """;

        var handlerMock = CreateHandlerMock();
        SetupHandlerResponse(handlerMock, HttpStatusCode.OK, json);

        var client = new YouTubeApiClient(
            CreateHttpClient(handlerMock),
            CreateTokenProviderMock().Object,
            quotaTracker: null,
            ResiliencePipeline<HttpResponseMessage>.Empty);

        // Act
        var result = await client.GetVideoMetadataAsync(["privVid1"]);

        // Assert
        result.Should().HaveCount(1);
        result[0].Status.Should().Be(VideoStatus.Private);
    }

    [Fact]
    public async Task Test_MoveVideo_AddsToTargetThenRemovesFromSource()
    {
        // Arrange
        var operations = new List<string>();
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        // POST for AddVideo
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                operations.Add("add");
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"id":"newItem"}""", Encoding.UTF8, "application/json")
                };
            });

        // DELETE for RemoveVideo
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Delete),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                operations.Add("remove");
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            });

        var client = new YouTubeApiClient(
            CreateHttpClient(handlerMock),
            CreateTokenProviderMock().Object,
            quotaTracker: null,
            ResiliencePipeline<HttpResponseMessage>.Empty);

        // Act
        await client.AddVideoToPlaylistAsync("targetPL", "vid1");
        await client.RemoveVideoFromPlaylistAsync("sourcePL", "itemId1");

        // Assert
        operations.Should().ContainInOrder("add", "remove");
    }

    [Fact]
    public async Task Test_QuotaExceeded_ThrowsQuotaExhaustedException()
    {
        // Arrange - 403 with quotaExceeded in body
        const string errorBody = """
            {
                "error": {
                    "code": 403,
                    "errors": [{ "reason": "quotaExceeded" }]
                }
            }
            """;

        var handlerMock = CreateHandlerMock();
        SetupHandlerResponse(handlerMock, HttpStatusCode.Forbidden, errorBody);

        var client = new YouTubeApiClient(
            CreateHttpClient(handlerMock),
            CreateTokenProviderMock().Object,
            quotaTracker: null,
            ResiliencePipeline<HttpResponseMessage>.Empty);

        // Act & Assert
        await client.Invoking(c => c.GetUserPlaylistsAsync())
            .Should().ThrowAsync<QuotaExhaustedException>();
    }

    [Fact]
    public async Task Test_RateLimiting_RetriesWithBackoff()
    {
        // Arrange - 429 twice, then 200 on third call → 3 total calls
        var callCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount < 3)
                {
                    return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"items":[]}""", Encoding.UTF8, "application/json")
                };
            });

        var retryPipeline = CreateZeroDelayRetryPipeline();
        var client = new YouTubeApiClient(
            CreateHttpClient(handlerMock),
            CreateTokenProviderMock().Object,
            quotaTracker: null,
            retryPipeline);

        // Act
        await client.GetUserPlaylistsAsync();

        // Assert
        callCount.Should().Be(3);
    }
}
