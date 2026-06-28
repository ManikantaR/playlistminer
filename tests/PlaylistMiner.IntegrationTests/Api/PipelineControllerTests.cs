using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;
using Xunit;

namespace PlaylistMiner.IntegrationTests.Api;

[Trait("Category", "Integration")]
public class PipelineControllerTests
{
    private static async Task SeedDbAsync(PlaylistMinerDbContext db, string runId)
    {
        var run = new PipelineRun
        {
            RunId = runId,
            PipelineType = "sync",
            Status = "completed",
            Phase = "completed",
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            UpdatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            CurrentMessage = "Run completed successfully.",
            PlaylistsDiscovered = 2,
            PlaylistsProcessed = 2,
            PlaylistItemsFetched = 10,
            UniqueVideoIdsIdentified = 10,
            VideosUpserted = 10
        };

        var ev1 = new PipelineEvent
        {
            RunId = runId,
            OccurredAt = DateTime.UtcNow.AddMinutes(-5),
            Level = "info",
            Phase = "starting",
            Message = "Pipeline run started."
        };

        var ev2 = new PipelineEvent
        {
            RunId = runId,
            OccurredAt = DateTime.UtcNow,
            Level = "info",
            Phase = "completed",
            Message = "Pipeline run completed successfully."
        };

        db.PipelineRuns.Add(run);
        db.PipelineEvents.AddRange(ev1, ev2);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Test_GetStatus_Returns200_WithLatestRun()
    {
        // Arrange
        using var factory = new PlaylistMinerWebAppFactory();
        var runId = Guid.NewGuid().ToString();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlaylistMinerDbContext>();
            await SeedDbAsync(db, runId);
        }
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/pipeline/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var run = await response.Content.ReadFromJsonAsync<PipelineRunDto>();
        run.Should().NotBeNull();
        run!.RunId.Should().Be(runId);
        run.PipelineType.Should().Be("sync");
        run.Status.Should().Be("completed");
        run.PlaylistsDiscovered.Should().Be(2);
    }

    [Fact]
    public async Task Test_GetHistory_ReturnsRecentRuns()
    {
        // Arrange
        using var factory = new PlaylistMinerWebAppFactory();
        var runId1 = Guid.NewGuid().ToString();
        var runId2 = Guid.NewGuid().ToString();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlaylistMinerDbContext>();
            await SeedDbAsync(db, runId1);
            await SeedDbAsync(db, runId2);
        }
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/pipeline/history");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var runs = await response.Content.ReadFromJsonAsync<List<PipelineRunDto>>();
        runs.Should().NotBeNull();
        runs.Should().HaveCount(2);
    }

    [Fact]
    public async Task Test_GetRunDetail_ReturnsRunDetail_OrNotFound()
    {
        // Arrange
        using var factory = new PlaylistMinerWebAppFactory();
        var runId = Guid.NewGuid().ToString();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlaylistMinerDbContext>();
            await SeedDbAsync(db, runId);
        }
        var client = factory.CreateClient();

        // Act & Assert 1: Success
        var responseOk = await client.GetAsync($"/api/pipeline/history/{runId}");
        responseOk.StatusCode.Should().Be(HttpStatusCode.OK);
        var run = await responseOk.Content.ReadFromJsonAsync<PipelineRunDto>();
        run.Should().NotBeNull();
        run!.RunId.Should().Be(runId);

        // Act & Assert 2: NotFound
        var responseNotFound = await client.GetAsync("/api/pipeline/history/non_existent_run_id");
        responseNotFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Test_GetEvents_ReturnsEventsForRun()
    {
        // Arrange
        using var factory = new PlaylistMinerWebAppFactory();
        var runId = Guid.NewGuid().ToString();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlaylistMinerDbContext>();
            await SeedDbAsync(db, runId);
        }
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/pipeline/events?runId={runId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var events = await response.Content.ReadFromJsonAsync<List<PipelineEventDto>>();
        events.Should().NotBeNull();
        events.Should().HaveCount(2);
        events![0].Phase.Should().Be("starting");
        events[1].Phase.Should().Be("completed");
    }

    [Fact]
    public async Task Test_GetHealth_Returns200_WithHealthStatus()
    {
        // Arrange
        using var factory = new PlaylistMinerWebAppFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/pipeline/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var health = await response.Content.ReadFromJsonAsync<DependencyHealthDto>();
        health.Should().NotBeNull();
        health!.Database.Should().Be("healthy");
        health.WorkerStatus.Should().Be("unknown");
    }
}
