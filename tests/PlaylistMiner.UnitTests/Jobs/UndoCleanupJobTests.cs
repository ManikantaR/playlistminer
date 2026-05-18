using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;
using PlaylistMiner.Worker.Jobs;
using Quartz;

namespace PlaylistMiner.UnitTests.Jobs;

[Trait("Category", "Unit")]
public class UndoCleanupJobTests
{
    private static PlaylistMinerDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<PlaylistMinerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlaylistMinerDbContext(options);
    }

    private static IJobExecutionContext CreateContext()
    {
        var mock = new Mock<IJobExecutionContext>();
        mock.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }

    private static Video MakeVideo(PlaylistMinerDbContext db)
    {
        var video = new Video
        {
            YouTubeId = Guid.NewGuid().ToString()[..11],
            Title = "Test",
            Description = "Desc",
            ChannelName = "Channel",
            ChannelId = "UC123",
            ThumbnailUrl = "https://thumb.jpg",
            Status = VideoStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow
        };
        db.Videos.Add(video);
        db.SaveChanges();
        return video;
    }

    [Fact]
    public async Task Test_Cleanup_DeletesExpiredEntries()
    {
        // Arrange
        using var db = CreateDb();
        var video = MakeVideo(db);
        var expiredLog = new UndoLog
        {
            VideoId = video.Id,
            Action = "move",
            PerformedAt = DateTime.UtcNow.AddDays(-2),
            ExpiresAt = DateTime.UtcNow.AddHours(-1), // expired
            Undone = false
        };
        db.UndoLogs.Add(expiredLog);
        await db.SaveChangesAsync();

        var logger = new Mock<ILogger<UndoCleanupJob>>();
        var job = new UndoCleanupJob(db, logger.Object);

        // Act
        await job.Execute(CreateContext());

        // Assert
        var remaining = await db.UndoLogs.CountAsync();
        remaining.Should().Be(0);
    }

    [Fact]
    public async Task Test_Cleanup_KeepsNonExpiredEntries()
    {
        // Arrange
        using var db = CreateDb();
        var video = MakeVideo(db);
        var futureLog = new UndoLog
        {
            VideoId = video.Id,
            Action = "move",
            PerformedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(1), // not expired
            Undone = false
        };
        db.UndoLogs.Add(futureLog);
        await db.SaveChangesAsync();

        var logger = new Mock<ILogger<UndoCleanupJob>>();
        var job = new UndoCleanupJob(db, logger.Object);

        // Act
        await job.Execute(CreateContext());

        // Assert
        var remaining = await db.UndoLogs.CountAsync();
        remaining.Should().Be(1);
    }
}
