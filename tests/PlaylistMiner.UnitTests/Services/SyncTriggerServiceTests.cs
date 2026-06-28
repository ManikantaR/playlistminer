using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;
using PlaylistMiner.Infrastructure.Services;

namespace PlaylistMiner.UnitTests.Services;

[Trait("Category", "Unit")]
public class SyncTriggerServiceTests
{
    private static PlaylistMinerDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<PlaylistMinerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlaylistMinerDbContext(options);
    }

    [Fact]
    public async Task Test_TriggerSync_WritesToDatabase()
    {
        // Arrange
        using var db = CreateDb();
        var service = new SyncTriggerService(db);

        // Act
        await service.TriggerAsync("full");

        // Assert
        var request = await db.SyncRequests.FirstOrDefaultAsync();
        request.Should().NotBeNull();
        request!.Type.Should().Be("full");
        request.Status.Should().Be("pending");
        request.RequestedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Test_GetPendingRequest_ReturnsOldestFirst()
    {
        // Arrange
        using var db = CreateDb();
        var service = new SyncTriggerService(db);

        var older = new SyncRequest
        {
            Type = "full",
            Status = "pending",
            RequestedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        var newer = new SyncRequest
        {
            Type = "inbox",
            Status = "pending",
            RequestedAt = DateTime.UtcNow
        };
        db.SyncRequests.AddRange(older, newer);
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetPendingRequestAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be("full"); // the older one
    }

    [Fact]
    public async Task Test_MarkCompleted_UpdatesStatus()
    {
        // Arrange
        using var db = CreateDb();
        var service = new SyncTriggerService(db);
        await service.TriggerAsync("full");
        var request = await db.SyncRequests.FirstAsync();

        // Act
        await service.MarkCompletedAsync(request.Id);

        // Assert
        await db.Entry(request).ReloadAsync();
        request.Status.Should().Be("completed");
        request.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Test_MarkProcessing_SetsStartedAt()
    {
        // Arrange
        using var db = CreateDb();
        var service = new SyncTriggerService(db);
        await service.TriggerAsync("full");
        var request = await db.SyncRequests.FirstAsync();

        // Act
        await service.MarkProcessingAsync(request.Id);

        // Assert
        await db.Entry(request).ReloadAsync();
        request.Status.Should().Be("processing");
        request.StartedAt.Should().NotBeNull();
    }
}
