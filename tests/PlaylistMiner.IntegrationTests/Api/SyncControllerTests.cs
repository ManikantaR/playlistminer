using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
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
        var mockTrigger = new Mock<ISyncTrigger>();
        mockTrigger.Setup(s => s.TriggerAsync("full", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var factory = new PlaylistMinerWebAppFactory(services =>
            services.AddSingleton(mockTrigger.Object));
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/sync/trigger", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        mockTrigger.Verify(s => s.TriggerAsync("full", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Test_GetStatus_Returns200_WithCurrentStatus()
    {
        // Arrange
        var mockTrigger = new Mock<ISyncTrigger>();
        using var factory = new PlaylistMinerWebAppFactory(services =>
            services.AddSingleton(mockTrigger.Object));
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
        var mockTrigger = new Mock<ISyncTrigger>();
        using var factory = new PlaylistMinerWebAppFactory(services =>
            services.AddSingleton(mockTrigger.Object));
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/sync/history");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var logs = await response.Content.ReadFromJsonAsync<List<SyncLog>>();
        logs.Should().NotBeNull();
    }
}
