using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;

namespace PlaylistMiner.IntegrationTests.Api;

[Trait("Category", "Integration")]
public class RemoteDuplicateCleanupControllerTests
{
    [Fact]
    public async Task Test_PlanRemoteCleanup_Returns200_WithPlannedRemovals()
    {
        // Arrange
        var mockService = new Mock<IRemoteDuplicateCleanupService>();
        mockService.Setup(s => s.BuildPlanAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new RemoteDuplicateCleanupItemDto(
                    10,
                    "vid001",
                    "Distributed Systems Deep Dive",
                    2,
                    "Distributed Systems",
                    false,
                    [
                        new RemoteDuplicateRemovalTargetDto(1, "Inbox", "pli-inbox")
                    ])
            ]);

        using var factory = new PlaylistMinerWebAppFactory(services =>
        {
            services.AddSingleton(mockService.Object);
        });
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/operations/duplicates/plan-remote-cleanup", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var plan = await response.Content.ReadFromJsonAsync<List<RemoteDuplicateCleanupItemDto>>();
        plan.Should().NotBeNull();
        plan.Should().HaveCount(1);
        plan![0].WinnerPlaylistName.Should().Be("Distributed Systems");
        plan[0].LoserPlaylists.Should().ContainSingle();
    }

    [Fact]
    public async Task Test_ExecuteRemoteCleanup_Returns200_WithSummary()
    {
        // Arrange
        var mockService = new Mock<IRemoteDuplicateCleanupService>();
        mockService.Setup(s => s.ExecuteAsync(It.IsAny<IEnumerable<RemoteDuplicateCleanupItemDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RemoteDuplicateCleanupResultDto(1, 1, 1, 0, 0, [], "run-123"));

        using var factory = new PlaylistMinerWebAppFactory(services =>
        {
            services.AddSingleton(mockService.Object);
        });
        var client = factory.CreateClient();

        var request = new List<RemoteDuplicateCleanupItemDto>
        {
            new(
                10,
                "vid001",
                "Distributed Systems Deep Dive",
                2,
                "Distributed Systems",
                false,
                [new RemoteDuplicateRemovalTargetDto(1, "Inbox", "pli-inbox")])
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/operations/duplicates/execute-remote-cleanup", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RemoteDuplicateCleanupResultDto>();
        result.Should().NotBeNull();
        result!.RemovalsExecuted.Should().Be(1);
        result.RunId.Should().Be("run-123");
    }

    [Fact]
    public async Task Test_ExecuteRemoteCleanup_WhenRequestExceedsRemovalLimit_Returns400ProblemDetails()
    {
        // Arrange
        var mockService = new Mock<IRemoteDuplicateCleanupService>(MockBehavior.Strict);

        using var factory = new PlaylistMinerWebAppFactory(services =>
        {
            services.AddSingleton(mockService.Object);
        });
        var client = factory.CreateClient();

        var request = Enumerable.Range(1, 26)
            .Select(index => new RemoteDuplicateCleanupItemDto(
                index,
                $"vid{index:D3}",
                $"Video {index}",
                200 + index,
                $"Winner {index}",
                false,
                [new RemoteDuplicateRemovalTargetDto(100 + index, $"Loser {index}", $"pli-{index:D3}")]))
            .ToList();

        // Act
        var response = await client.PostAsJsonAsync("/api/operations/duplicates/execute-remote-cleanup", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Remote cleanup request exceeds the allowed batch size.");
        problem.Detail.Should().Contain("25");
    }

    [Fact]
    public async Task Test_ExecuteRemoteCleanup_WhenPlanHasUnresolvedRemovals_Returns400ProblemDetails()
    {
        // Arrange
        var mockService = new Mock<IRemoteDuplicateCleanupService>(MockBehavior.Strict);

        using var factory = new PlaylistMinerWebAppFactory(services =>
        {
            services.AddSingleton(mockService.Object);
        });
        var client = factory.CreateClient();

        var request = new List<RemoteDuplicateCleanupItemDto>
        {
            new(
                10,
                "vid001",
                "Distributed Systems Deep Dive",
                2,
                "Distributed Systems",
                true,
                [new RemoteDuplicateRemovalTargetDto(1, "Inbox", null)])
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/operations/duplicates/execute-remote-cleanup", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Remote cleanup plan has unresolved removals.");
        problem.Detail.Should().Contain("playlist item ids");
    }
}
