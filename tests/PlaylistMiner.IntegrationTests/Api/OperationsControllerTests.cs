using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public async Task Test_GetHealth_Returns200_WithCorrectProperties()
    {
        // Arrange
        using var factory = new PlaylistMinerWebAppFactory();
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
}
