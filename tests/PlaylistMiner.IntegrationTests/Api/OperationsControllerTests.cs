using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;
using Xunit;

namespace PlaylistMiner.IntegrationTests.Api;

[Trait("Category", "Integration")]
public class OperationsControllerTests
{
    private static async Task SeedRunAsync(PlaylistMinerDbContext db, string runId, string status, DateTime updatedAt, string phase)
    {
        var run = new PipelineRun
        {
            RunId = runId,
            PipelineType = "sync",
            Status = status,
            Phase = phase,
            StartedAt = DateTime.UtcNow.AddMinutes(-20),
            UpdatedAt = updatedAt,
            CurrentMessage = "Working..."
        };
        db.PipelineRuns.Add(run);
        await db.SaveChangesAsync();
    }

    private static PlaylistMinerWebAppFactory CreateFactoryWithOllamaReachable(bool reachable)
    {
        return new PlaylistMinerWebAppFactory(services =>
        {
            var ollamaMock = new Mock<IOllamaCategorizer>();
            ollamaMock.Setup(o => o.IsAvailableAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(reachable);

            services.RemoveAll<IOllamaCategorizer>();
            services.AddSingleton(ollamaMock.Object);
        });
    }

    [Fact]
    public async Task Test_GetHealth_Returns200_WithCorrectProperties()
    {
        // Arrange
        using var factory = CreateFactoryWithOllamaReachable(false);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/operations/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("apiHealthy").GetBoolean().Should().BeTrue();
        root.GetProperty("dbHealthy").GetBoolean().Should().BeTrue();
        root.GetProperty("workerHealthy").GetBoolean().Should().BeFalse(); // No heartbeat seeded
        root.GetProperty("workerHeartbeatAgeSeconds").GetInt32().Should().Be(-1);
        root.GetProperty("oauthConnected").GetBoolean().Should().BeFalse();
        root.GetProperty("quotaExhausted").GetBoolean().Should().BeFalse();
        root.GetProperty("ollamaReachable").GetBoolean().Should().BeFalse();
        root.GetProperty("activeRunStalled").GetBoolean().Should().BeFalse();
        root.TryGetProperty("activeRunPhase", out var phaseProp).Should().BeTrue();
        phaseProp.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Test_GetHealth_DetectsStalledRun()
    {
        // Arrange
        using var factory = new PlaylistMinerWebAppFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlaylistMinerDbContext>();
            // Seed a run updated 10 minutes ago (which exceeds default 5 min stall threshold)
            await SeedRunAsync(db, "stalled-run-id", "in_progress", DateTime.UtcNow.AddMinutes(-10), "hydrating_video_metadata");
        }
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/operations/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("activeRunStalled").GetBoolean().Should().BeTrue();
        root.GetProperty("activeRunPhase").GetString().Should().Be("hydrating_video_metadata");
    }

    [Fact]
    public async Task Test_GetHealth_DetectsActiveRunNotStalled()
    {
        // Arrange
        using var factory = new PlaylistMinerWebAppFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlaylistMinerDbContext>();
            // Seed a run updated 1 minute ago (which is within default 5 min threshold)
            await SeedRunAsync(db, "recent-run-id", "in_progress", DateTime.UtcNow.AddMinutes(-1), "fetching_playlists");
        }
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/operations/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("activeRunStalled").GetBoolean().Should().BeFalse();
        root.GetProperty("activeRunPhase").GetString().Should().Be("fetching_playlists");
    }

    [Fact]
    public async Task Test_GetDuplicates_ReturnsDuplicateReviewItems()
    {
        // Arrange
        var mockOrganizer = new Mock<IPlaylistOrganizer>();
        mockOrganizer.Setup(o => o.GetDuplicateReviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new DuplicateReviewDto(
                    42,
                    "dupvideo01",
                    "Distributed Systems Deep Dive",
                    "https://example.com/dup.jpg",
                    2,
                    [
                        new DuplicatePlaylistDto(7, "AI Agents", true, "AI Agents"),
                        new DuplicatePlaylistDto(8, "Backend Systems", true, "Backend Systems")
                    ])
            ]);

        using var factory = new PlaylistMinerWebAppFactory(services =>
        {
            services.AddSingleton(mockOrganizer.Object);
        });
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/operations/duplicates");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var duplicates = await response.Content.ReadFromJsonAsync<List<DuplicateReviewDto>>();
        duplicates.Should().NotBeNull();
        duplicates.Should().HaveCount(1);
        duplicates![0].VideoId.Should().Be(42);
        duplicates[0].Playlists.Should().HaveCount(2);
    }

    [Fact]
    public async Task Test_GetActivity_ReturnsPagedNewestFirstItems()
    {
        // Arrange
        using var factory = new PlaylistMinerWebAppFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlaylistMinerDbContext>();
            var now = DateTime.UtcNow;

            db.PipelineRuns.AddRange(
                new PipelineRun
                {
                    RunId = "run-activity-1",
                    PipelineType = "remote-duplicate-cleanup",
                    Status = "completed",
                    Phase = "completed",
                    StartedAt = now.AddMinutes(-10),
                    UpdatedAt = now.AddMinutes(-8),
                    CompletedAt = now.AddMinutes(-8)
                },
                new PipelineRun
                {
                    RunId = "run-activity-2",
                    PipelineType = "sync",
                    Status = "failed",
                    Phase = "failed",
                    StartedAt = now.AddMinutes(-20),
                    UpdatedAt = now.AddMinutes(-18),
                    CompletedAt = now.AddMinutes(-18)
                });

            db.PipelineEvents.AddRange(
                new PipelineEvent
                {
                    RunId = "run-activity-1",
                    OccurredAt = now.AddMinutes(-3),
                    Level = "info",
                    Phase = "completed",
                    Message = "Removed duplicate video from playlist \"Inbox\"."
                },
                new PipelineEvent
                {
                    RunId = "run-activity-2",
                    OccurredAt = now.AddMinutes(-2),
                    Level = "error",
                    Phase = "failed",
                    Message = "Sync failed due to token refresh error."
                });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/operations/activity?limit=1&offset=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<OperationsActivityFeedDto>();
        payload.Should().NotBeNull();
        payload!.Items.Should().HaveCount(1);
        payload.TotalCount.Should().Be(1);
        payload.HasMore.Should().BeFalse();
        payload.Items[0].PipelineType.Should().Be("remote-duplicate-cleanup");
        payload.Items[0].PipelineLabel.Should().Be("Remote Cleanup");
        payload.Items[0].Message.Should().Be("Removed duplicate video from playlist \"Inbox\".");
    }

    [Fact]
    public async Task Test_GetQuota_ReturnsMoveBudgetSnapshot()
    {
        // Arrange
        using var factory = new PlaylistMinerWebAppFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlaylistMinerDbContext>();
            var now = DateTime.UtcNow;

            db.PipelineRuns.Add(new PipelineRun
            {
                RunId = "run-quota-1",
                PipelineType = "remote-duplicate-cleanup",
                Status = "completed",
                Phase = "completed",
                StartedAt = now.AddMinutes(-20),
                UpdatedAt = now.AddMinutes(-10),
                CompletedAt = now.AddMinutes(-10),
                VideosProcessed = 12
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/operations/quota");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<OperationsQuotaDto>();
        payload.Should().NotBeNull();
        payload!.MovesUsedToday.Should().Be(12);
        payload.MoveBudget.Should().BeGreaterThan(0);
        payload.UnitsRemaining.Should().Be(payload.MoveBudget - 12);
    }
}
