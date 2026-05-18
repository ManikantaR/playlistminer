using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using PlaylistMiner.Infrastructure.Data;
using PlaylistMiner.Infrastructure.YouTube;

namespace PlaylistMiner.UnitTests.YouTube;

[Trait("Category", "Unit")]
public class OAuthTokenProviderTests
{
    private static PlaylistMinerDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<PlaylistMinerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlaylistMinerDbContext(options);
    }

    private static IOptions<YouTubeSettings> CreateSettings(string? encryptionKey = null)
    {
        // 32-byte key encoded as base64
        var key = encryptionKey ?? Convert.ToBase64String(new byte[32]);
        return Options.Create(new YouTubeSettings
        {
            ApiKey = "test-api-key",
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            RedirectUri = "http://localhost:8888/oauth/callback",
            EncryptionKey = key
        });
    }

    private static Mock<HttpMessageHandler> CreateHandlerMock(
        HttpStatusCode statusCode, string responseJson)
    {
        var mock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        mock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        return mock;
    }

    private static Mock<IHttpClientFactory> CreateHttpClientFactory(
        Mock<HttpMessageHandler> handlerMock)
    {
        var httpClient = new HttpClient(handlerMock.Object);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);
        return factory;
    }

    [Fact]
    public async Task Test_GetAccessToken_ReturnsCachedToken_WhenNotExpired()
    {
        // Arrange
        var handlerMock = CreateHandlerMock(HttpStatusCode.OK, """
            {
                "access_token": "fresh-token",
                "expires_in": 3600,
                "token_type": "Bearer"
            }
            """);
        var factory = CreateHttpClientFactory(handlerMock);
        using var db = CreateInMemoryDbContext();

        // Store a valid refresh token
        var settings = CreateSettings();
        var provider = new OAuthTokenProvider(settings, db, factory.Object);

        // Store a refresh token by pre-seeding the DB with an encrypted value
        // First, exchange a code to populate the token
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                        "access_token": "cached-token",
                        "refresh_token": "my-refresh-token",
                        "expires_in": 3600,
                        "token_type": "Bearer"
                    }
                    """, Encoding.UTF8, "application/json")
            });

        await provider.ExchangeCodeAsync("auth-code");

        // Act - call twice; second call should use cache, not HTTP
        var token1 = await provider.GetAccessTokenAsync();
        var token2 = await provider.GetAccessTokenAsync();

        // Assert
        token1.Should().Be("cached-token");
        token2.Should().Be("cached-token");

        // HTTP was only called once (for exchange), not again for the second GetAccessToken
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task Test_GetAccessToken_RefreshesToken_WhenExpired()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var settings = CreateSettings();

        // Seed an exchange to store a refresh token
        var exchangeHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var callCount = 0;
        exchangeHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1
                    ? new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""
                            {
                                "access_token": "initial-token",
                                "refresh_token": "stored-refresh",
                                "expires_in": 0,
                                "token_type": "Bearer"
                            }
                            """, Encoding.UTF8, "application/json")
                    }
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""
                            {
                                "access_token": "refreshed-token",
                                "expires_in": 3600,
                                "token_type": "Bearer"
                            }
                            """, Encoding.UTF8, "application/json")
                    };
            });

        var factory = CreateHttpClientFactory(exchangeHandlerMock);
        var provider = new OAuthTokenProvider(settings, db, factory.Object);

        await provider.ExchangeCodeAsync("auth-code");

        // Act - token is expired (expires_in=0), so refresh should occur
        var token = await provider.GetAccessTokenAsync();

        // Assert - should have refreshed
        token.Should().Be("refreshed-token");
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task Test_GetAccessToken_ThrowsWhenRefreshFails()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var settings = CreateSettings();

        var callCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1
                    ? new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""
                            {
                                "access_token": "first-token",
                                "refresh_token": "bad-refresh",
                                "expires_in": 0,
                                "token_type": "Bearer"
                            }
                            """, Encoding.UTF8, "application/json")
                    }
                    : new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        Content = new StringContent("""{"error":"invalid_grant"}""", Encoding.UTF8, "application/json")
                    };
            });

        var factory = CreateHttpClientFactory(handlerMock);
        var provider = new OAuthTokenProvider(settings, db, factory.Object);

        await provider.ExchangeCodeAsync("auth-code");

        // Act & Assert
        await provider.Invoking(p => p.GetAccessTokenAsync())
            .Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public void Test_InitiateOAuthFlow_ReturnsAuthorizationUrl()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var settings = CreateSettings();
        var factory = new Mock<IHttpClientFactory>().Object;
        var provider = new OAuthTokenProvider(settings, db, factory);

        // Act
        var url = provider.GetAuthorizationUrl();

        // Assert
        url.Should().Contain("client_id=test-client-id");
        url.Should().Contain("scope=");
        url.Should().Contain("youtube");
        url.Should().Contain("redirect_uri=");
        url.Should().Contain("localhost");
    }

    [Fact]
    public async Task Test_ExchangeCode_StoresRefreshToken()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var settings = CreateSettings();

        var handlerMock = CreateHandlerMock(HttpStatusCode.OK, """
            {
                "access_token": "new-access-token",
                "refresh_token": "new-refresh-token",
                "expires_in": 3600,
                "token_type": "Bearer"
            }
            """);
        var factory = CreateHttpClientFactory(handlerMock);
        var provider = new OAuthTokenProvider(settings, db, factory.Object);

        // Act
        await provider.ExchangeCodeAsync("auth-code-xyz");

        // Assert - refresh token stored encrypted in DB
        var isConnected = await provider.IsConnectedAsync();
        isConnected.Should().BeTrue();

        var setting = await db.Settings.FindAsync("oauth.refresh_token");
        setting.Should().NotBeNull();
        setting!.Value.Should().NotBe("new-refresh-token"); // must be encrypted
    }
}
