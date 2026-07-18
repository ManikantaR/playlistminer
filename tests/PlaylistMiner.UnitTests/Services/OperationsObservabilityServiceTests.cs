using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;
using PlaylistMiner.Infrastructure.Services;
using Xunit;

namespace PlaylistMiner.UnitTests.Services;

[Trait("Category", "Unit")]
public class OperationsObservabilityServiceTests
{
    private static PlaylistMinerDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<PlaylistMinerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlaylistMinerDbContext(options);
    }

    [Fact]
    public async Task Test_GetActivityAsync_ReturnsNewestFirstPageWithPagination()
    {
        // Arrange
        using var db = CreateDb();
        var now = new DateTime(2026, 7, 4, 15, 0, 0, DateTimeKind.Utc);

        db.PipelineRuns.AddRange(
            new PipelineRun
            {
                RunId = "run-1",
                PipelineType = "remote-duplicate-cleanup",
                Status = "completed",
                Phase = "completed",
                StartedAt = now.AddMinutes(-10),
                UpdatedAt = now.AddMinutes(-8),
                CompletedAt = now.AddMinutes(-8)
            },
            new PipelineRun
            {
                RunId = "run-2",
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
                RunId = "run-1",
                OccurredAt = now.AddMinutes(-7),
                Level = "info",
                Phase = "completed",
                Message = "Removed duplicate video from playlist \"Inbox\"."
            },
            new PipelineEvent
            {
                RunId = "run-2",
                OccurredAt = now.AddMinutes(-6),
                Level = "error",
                Phase = "failed",
                Message = "Sync failed due to token refresh error."
            },
            new PipelineEvent
            {
                RunId = "run-1",
                OccurredAt = now.AddMinutes(-5),
                Level = "warning",
                Phase = "executing",
                Message = "Skipped one removal because the winner playlist changed."
            });
        await db.SaveChangesAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Organize:DailyMoveBudget"] = "80"
            })
            .Build();
        var service = new OperationsObservabilityService(
            db,
            configuration,
            new FakeTimeProvider(now));

        // Act
        var result = await service.GetActivityAsync(limit: 2, offset: 0);

        // Assert
        result.TotalCount.Should().Be(2);
        result.HasMore.Should().BeFalse();
        result.Items.Should().HaveCount(2);
        result.Items[0].Message.Should().Be("Skipped one removal because the winner playlist changed.");
        result.Items[0].PipelineType.Should().Be("remote-duplicate-cleanup");
        result.Items[0].PipelineLabel.Should().Be("Remote Cleanup");
        result.Items[1].Message.Should().Be("Removed duplicate video from playlist \"Inbox\".");
    }

    [Fact]
    public async Task Test_GetMoveBudgetAsync_UsesCurrentPacificDayWindow()
    {
        // Arrange
        using var db = CreateDb();
        var now = new DateTime(2026, 7, 4, 15, 0, 0, DateTimeKind.Utc);

        db.PipelineRuns.AddRange(
            new PipelineRun
            {
                RunId = "cleanup-today",
                PipelineType = "remote-duplicate-cleanup",
                Status = "completed",
                Phase = "completed",
                StartedAt = now.AddHours(-2),
                UpdatedAt = now.AddHours(-1),
                CompletedAt = now.AddHours(-1),
                VideosProcessed = 34
            },
            new PipelineRun
            {
                RunId = "cleanup-yesterday",
                PipelineType = "remote-duplicate-cleanup",
                Status = "completed",
                Phase = "completed",
                StartedAt = now.AddDays(-1),
                UpdatedAt = now.AddDays(-1),
                CompletedAt = now.AddDays(-1),
                VideosProcessed = 12
            },
            new PipelineRun
            {
                RunId = "sync-today",
                PipelineType = "sync",
                Status = "completed",
                Phase = "completed",
                StartedAt = now.AddHours(-3),
                UpdatedAt = now.AddHours(-3),
                CompletedAt = now.AddHours(-3),
                VideosProcessed = 99
            });
        await db.SaveChangesAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Organize:DailyMoveBudget"] = "80"
            })
            .Build();
        var service = new OperationsObservabilityService(
            db,
            configuration,
            new FakeTimeProvider(now));

        // Act
        var result = await service.GetMoveBudgetAsync();

        // Assert
        result.MovesUsedToday.Should().Be(34);
        result.MoveBudget.Should().Be(80);
        result.UnitsRemaining.Should().Be(46);
        result.IsBlocked.Should().BeFalse();
        result.ResetsAt.Should().BeAfter(now);
    }

    [Fact]
    public async Task Test_GetMoveBudgetAsync_UsesPersistedAutomationPolicyBudget()
    {
        // Arrange
        using var db = CreateDb();
        var now = new DateTime(2026, 7, 4, 15, 0, 0, DateTimeKind.Utc);
        db.Settings.Add(new Setting
        {
            Key = "automation.daily_move_budget",
            Value = "42",
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        var configuration = new ConfigurationBuilder().Build();
        var service = new OperationsObservabilityService(
            db,
            configuration,
            new FakeTimeProvider(now));

        // Act
        var result = await service.GetMoveBudgetAsync();

        // Assert
        result.MoveBudget.Should().Be(42);
        result.UnitsRemaining.Should().Be(42);
    }

    private sealed class FakeTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
        public override long GetTimestamp() => utcNow.Ticks;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
            => throw new NotSupportedException();
    }
}
