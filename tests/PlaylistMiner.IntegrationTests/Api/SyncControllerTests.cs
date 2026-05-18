using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;

namespace PlaylistMiner.IntegrationTests.Api;

[Trait("Category", "Integration")]
public class SyncControllerTests
{
    [Fact]
    public async Task Test_TriggerSync_Returns202_Accepted()
    {
        // Arrange
        var mockSync = new Mock<ISyncService>();
        mockSync.Setup(s => s.FullSyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResult(0, 0, [], 0));

        using var factory = new PlaylistMinerWebAppFactory(services =>
            services.AddSingleton(mockSync.Object));
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/sync/trigger", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Test_GetStatus_Returns200_WithCurrentStatus()
    {
        // Arrange
        using var factory = new PlaylistMinerWebAppFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/sync/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Test_GetHistory_Returns200_WithSyncLogs()
    {
        // Arrange
        using var factory = new PlaylistMinerWebAppFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/sync/history");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var logs = await response.Content.ReadFromJsonAsync<List<SyncLog>>();
        logs.Should().NotBeNull();
    }
}
