using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;
using PlaylistMiner.Infrastructure.Services;
using Xunit;

namespace PlaylistMiner.UnitTests.Services;

[Trait("Category", "Unit")]
public class OperationQueueServiceTests
{
    private static PlaylistMinerDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<PlaylistMinerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlaylistMinerDbContext(options);
    }

    [Fact]
    public async Task Test_QueueAsync_WithValidRequest_PersistsScheduledOperation()
    {
        // Arrange
        using var db = CreateDb();
        var service = new OperationQueueService(db);
        var request = new CreateOperationRequestDto(
            Type: "inbox_sync",
            Source: "myinbox",
            Target: null,
            MaxItems: 25,
            QuotaEstimate: 100,
            NotBefore: new DateTime(2026, 7, 19, 3, 0, 0, DateTimeKind.Utc),
            AllowedWindowStart: "23:00",
            AllowedWindowEnd: "05:00");

        // Act
        var queued = await service.QueueAsync(request);

        // Assert
        queued.Status.Should().Be("scheduled");
        queued.Type.Should().Be("inbox_sync");
        queued.MaxItems.Should().Be(25);
        queued.QuotaEstimate.Should().Be(100);
        queued.AllowedWindowStart.Should().Be("23:00");
        queued.AllowedWindowEnd.Should().Be("05:00");

        var persisted = await db.OperationRequests.SingleAsync();
        persisted.Status.Should().Be("scheduled");
        persisted.Source.Should().Be("myinbox");
    }

    [Fact]
    public async Task Test_GetNextRunnableAsync_WhenOutsideAllowedWindow_DefersOperation()
    {
        // Arrange
        using var db = CreateDb();
        var operation = new OperationRequest
        {
            Type = "full_sync",
            Status = "queued",
            CreatedBy = "user",
            CreatedAt = new DateTime(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc),
            AllowedWindowStart = "23:00",
            AllowedWindowEnd = "05:00"
        };
        db.OperationRequests.Add(operation);
        await db.SaveChangesAsync();
        var service = new OperationQueueService(db);

        // Act
        var runnable = await service.GetNextRunnableAsync(new DateTime(2026, 7, 18, 16, 0, 0, DateTimeKind.Utc));

        // Assert
        runnable.Should().BeNull();
        var deferred = await db.OperationRequests.SingleAsync();
        deferred.Status.Should().Be("deferred");
        deferred.Error.Should().Contain("outside allowed execution window");
    }

    [Fact]
    public async Task Test_GetNextRunnableAsync_WhenInsideAllowedWindow_MarksRunning()
    {
        // Arrange
        using var db = CreateDb();
        db.OperationRequests.Add(new OperationRequest
        {
            Type = "full_sync",
            Status = "queued",
            CreatedBy = "user",
            CreatedAt = new DateTime(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc),
            AllowedWindowStart = "23:00",
            AllowedWindowEnd = "05:00"
        });
        await db.SaveChangesAsync();
        var service = new OperationQueueService(db);

        // Act
        var runnable = await service.GetNextRunnableAsync(new DateTime(2026, 7, 18, 23, 30, 0, DateTimeKind.Utc));

        // Assert
        runnable.Should().NotBeNull();
        runnable!.Status.Should().Be("running");
        runnable.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Test_GetNextRunnableAsync_WhenEarlierOperationScheduledInFuture_RunsLaterEligibleOperation()
    {
        // Arrange
        using var db = CreateDb();
        db.OperationRequests.AddRange(
            new OperationRequest
            {
                Type = "full_sync",
                Status = "scheduled",
                CreatedBy = "user",
                CreatedAt = new DateTime(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc),
                NotBefore = new DateTime(2026, 7, 19, 12, 0, 0, DateTimeKind.Utc)
            },
            new OperationRequest
            {
                Type = "inbox_sync",
                Status = "queued",
                CreatedBy = "user",
                CreatedAt = new DateTime(2026, 7, 18, 12, 1, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 7, 18, 12, 1, 0, DateTimeKind.Utc)
            });
        await db.SaveChangesAsync();
        var service = new OperationQueueService(db);

        // Act
        var runnable = await service.GetNextRunnableAsync(new DateTime(2026, 7, 18, 12, 5, 0, DateTimeKind.Utc));

        // Assert
        runnable.Should().NotBeNull();
        runnable!.Type.Should().Be("inbox_sync");
        runnable.Status.Should().Be("running");
    }

    [Fact]
    public async Task Test_CancelAsync_WhenQueued_CancelsOperation()
    {
        // Arrange
        using var db = CreateDb();
        var operation = new OperationRequest
        {
            Type = "full_sync",
            Status = "queued",
            CreatedBy = "user",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.OperationRequests.Add(operation);
        await db.SaveChangesAsync();
        var service = new OperationQueueService(db);

        // Act
        var canceled = await service.CancelAsync(operation.Id);

        // Assert
        canceled.Should().NotBeNull();
        canceled!.Status.Should().Be("canceled");
        canceled.CompletedAt.Should().NotBeNull();
    }
}
