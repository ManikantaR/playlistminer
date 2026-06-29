using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;
using PlaylistMiner.Infrastructure.Services;
using Xunit;

namespace PlaylistMiner.UnitTests.Services;

[Trait("Category", "Unit")]
public class PipelineRunTrackerTests
{
    private static PlaylistMinerDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<PlaylistMinerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlaylistMinerDbContext(options);
    }

    [Fact]
    public async Task Test_StartRunAsync_CreatesRunAndEvent()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var tracker = new PipelineRunTracker(db);

        // Act
        var runId = await tracker.StartRunAsync("sync");

        // Assert
        runId.Should().NotBeNullOrEmpty();
        var run = await db.PipelineRuns.FirstOrDefaultAsync(r => r.RunId == runId);
        run.Should().NotBeNull();
        run!.PipelineType.Should().Be("sync");
        run.Status.Should().Be("in_progress");
        run.Phase.Should().Be("starting");

        var events = await db.PipelineEvents.Where(e => e.RunId == runId).ToListAsync();
        events.Should().HaveCount(1);
        events[0].Phase.Should().Be("starting");
        events[0].Message.Should().Be("Pipeline run started.");
    }

    [Fact]
    public async Task Test_UpdateRunAsync_UpdatesCountersAndPhase()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var tracker = new PipelineRunTracker(db);
        var runId = await tracker.StartRunAsync("sync");

        // Act
        await tracker.UpdateRunAsync(runId, r =>
        {
            r.PlaylistsDiscovered = 5;
            r.PlaylistsProcessed = 2;
        }, phase: "fetching_playlist_items", message: "Fetching next playlist...");

        // Assert
        var run = await db.PipelineRuns.FirstOrDefaultAsync(r => r.RunId == runId);
        run!.PlaylistsDiscovered.Should().Be(5);
        run.PlaylistsProcessed.Should().Be(2);
        run.Phase.Should().Be("fetching_playlist_items");
        run.CurrentMessage.Should().Be("Fetching next playlist...");

        var events = await db.PipelineEvents.Where(e => e.RunId == runId).ToListAsync();
        // 1 for start, 1 for update since phase and message changed
        events.Should().HaveCount(2);
        events[1].Phase.Should().Be("fetching_playlist_items");
        events[1].Message.Should().Be("Fetching next playlist...");
    }

    [Fact]
    public async Task Test_CompleteRunAsync_MarksCompleted()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var tracker = new PipelineRunTracker(db);
        var runId = await tracker.StartRunAsync("sync");

        // Act
        await tracker.CompleteRunAsync(runId);

        // Assert
        var run = await db.PipelineRuns.FirstOrDefaultAsync(r => r.RunId == runId);
        run!.Status.Should().Be("completed");
        run.Phase.Should().Be("completed");
        run.CompletedAt.Should().NotBeNull();

        var events = await db.PipelineEvents.Where(e => e.RunId == runId).ToListAsync();
        events.Should().HaveCount(2);
        events.Last().Phase.Should().Be("completed");
    }

    [Fact]
    public async Task Test_FailRunAsync_MarksFailed()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var tracker = new PipelineRunTracker(db);
        var runId = await tracker.StartRunAsync("sync");

        // Act
        await tracker.FailRunAsync(runId, "Some critical error");

        // Assert
        var run = await db.PipelineRuns.FirstOrDefaultAsync(r => r.RunId == runId);
        run!.Status.Should().Be("failed");
        run.Phase.Should().Be("failed");
        run.Error.Should().Be("Some critical error");
        run.CompletedAt.Should().NotBeNull();

        var events = await db.PipelineEvents.Where(e => e.RunId == runId).ToListAsync();
        events.Should().HaveCount(2);
        events.Last().Level.Should().Be("error");
        events.Last().Message.Should().Contain("Some critical error");
    }

    [Fact]
    public async Task Test_DeferRunAsync_MarksDeferred()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var tracker = new PipelineRunTracker(db);
        var runId = await tracker.StartRunAsync("sync");

        // Act
        await tracker.DeferRunAsync(runId, "Quota limit reached");

        // Assert
        var run = await db.PipelineRuns.FirstOrDefaultAsync(r => r.RunId == runId);
        run!.Status.Should().Be("deferred");
        run.Phase.Should().Be("deferred");
        run.Error.Should().Be("Quota limit reached");
        run.CompletedAt.Should().NotBeNull();

        var events = await db.PipelineEvents.Where(e => e.RunId == runId).ToListAsync();
        events.Should().HaveCount(2);
        events.Last().Level.Should().Be("warning");
        events.Last().Message.Should().Contain("Quota limit reached");
    }

    [Fact]
    public async Task Test_ReapStaleRunsAsync_FailsStalledRunAndSyncLog()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var tracker = new PipelineRunTracker(db);

        db.PipelineRuns.Add(new PipelineRun
        {
            RunId = "stale-1",
            PipelineType = "sync",
            Status = "in_progress",
            Phase = "linking_playlist_items",
            StartedAt = DateTime.UtcNow.AddHours(-2),
            UpdatedAt = DateTime.UtcNow.AddHours(-2)
        });
        db.SyncLogs.Add(new SyncLog
        {
            SyncType = "Full",
            Status = "InProgress",
            StartedAt = DateTime.UtcNow.AddHours(-2)
        });
        await db.SaveChangesAsync();

        // Act
        var reaped = await tracker.ReapStaleRunsAsync(TimeSpan.FromMinutes(15));

        // Assert
        reaped.Should().Be(1);
        var run = await db.PipelineRuns.FirstAsync(r => r.RunId == "stale-1");
        run.Status.Should().Be("failed");
        run.Phase.Should().Be("stalled");
        run.CompletedAt.Should().NotBeNull();

        var log = await db.SyncLogs.FirstAsync();
        log.Status.Should().Be("Failed");
        log.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Test_ReapStaleRunsAsync_LeavesFreshRunsAndHeartbeatAlone()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var tracker = new PipelineRunTracker(db);

        db.PipelineRuns.AddRange(
            new PipelineRun
            {
                RunId = "fresh", PipelineType = "sync", Status = "in_progress", Phase = "processing_playlists",
                StartedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            },
            new PipelineRun
            {
                RunId = "worker-heartbeat", PipelineType = "worker", Status = "active", Phase = "heartbeat",
                StartedAt = DateTime.UtcNow.AddHours(-5), UpdatedAt = DateTime.UtcNow.AddHours(-5)
            });
        await db.SaveChangesAsync();

        // Act
        var reaped = await tracker.ReapStaleRunsAsync(TimeSpan.FromMinutes(15));

        // Assert — fresh run untouched; heartbeat marker never reaped even when old.
        reaped.Should().Be(0);
        (await db.PipelineRuns.FirstAsync(r => r.RunId == "fresh")).Status.Should().Be("in_progress");
        (await db.PipelineRuns.FirstAsync(r => r.RunId == "worker-heartbeat")).Status.Should().Be("active");
    }
}
