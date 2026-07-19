using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PlaylistMiner.Core.Exceptions;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;
using PlaylistMiner.Infrastructure.Services;

namespace PlaylistMiner.UnitTests.Services;

[Trait("Category", "Unit")]
public class PlaylistRestoreServiceTests
{
    private static PlaylistMinerDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<PlaylistMinerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlaylistMinerDbContext(opts);
    }

    [Fact]
    public async Task Test_RestoreSample_AddsMissingVideosToTargetPlaylistInSourceOrder()
    {
        using var db = CreateDb();
        SeedRestoreData(db);

        var ytMock = new Mock<IYouTubeApiClient>(MockBehavior.Strict);
        ytMock.Setup(y => y.AddVideoToPlaylistAsync("PLtarget", "yt-first", 11, It.IsAny<CancellationToken>()))
            .ReturnsAsync("pli-first");
        ytMock.Setup(y => y.AddVideoToPlaylistAsync("PLtarget", "yt-third", 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync("pli-third");

        var service = CreateService(db, ytMock);

        var result = await service.RestoreSampleAsync(1, 2, 2);

        result.AddedCount.Should().Be(2);
        result.SkippedCount.Should().Be(1);
        result.Added.Select(i => i.YouTubeId).Should().Equal("yt-first", "yt-third");

        var sourceLinks = await db.PlaylistVideos
            .Where(pv => pv.PlaylistId == 1)
            .OrderBy(pv => pv.VideoId)
            .ToListAsync();
        sourceLinks.Select(pv => pv.VideoId).Should().Equal(2);

        var targetLinks = await db.PlaylistVideos
            .Where(pv => pv.PlaylistId == 2)
            .OrderBy(pv => pv.Position)
            .ToListAsync();
        targetLinks.Select(pv => pv.VideoId).Should().Equal(2, 1, 3);
        targetLinks.Single(pv => pv.VideoId == 1).PlaylistItemId.Should().Be("pli-first");
        targetLinks.Single(pv => pv.VideoId == 3).PlaylistItemId.Should().Be("pli-third");

        ytMock.VerifyAll();
    }

    [Fact]
    public async Task Test_RestoreBatch_AllowsNightlyRestoreBudget()
    {
        using var db = CreateDb();
        SeedRestoreData(db);

        var ytMock = new Mock<IYouTubeApiClient>(MockBehavior.Strict);
        ytMock.Setup(y => y.AddVideoToPlaylistAsync("PLtarget", "yt-first", 11, It.IsAny<CancellationToken>()))
            .ReturnsAsync("pli-first");
        ytMock.Setup(y => y.AddVideoToPlaylistAsync("PLtarget", "yt-third", 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync("pli-third");

        var service = CreateService(db, ytMock);

        var result = await service.RestoreBatchAsync(1, 2, 150);

        result.RequestedCount.Should().Be(150);
        result.AddedCount.Should().Be(2);
        result.SkippedCount.Should().Be(1);
        result.Added.Select(i => i.YouTubeId).Should().Equal("yt-first", "yt-third");

        ytMock.VerifyAll();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public async Task Test_RestoreBatch_WhenBatchSizeUnsafe_ThrowsArgumentOutOfRangeException(int maxCount)
    {
        using var db = CreateDb();
        var service = CreateService(db);

        await service.Invoking(s => s.RestoreBatchAsync(1, 2, maxCount))
            .Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithMessage("*between 1 and 500*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(26)]
    public async Task Test_RestoreSample_WhenBatchSizeUnsafe_ThrowsArgumentOutOfRangeException(int maxCount)
    {
        using var db = CreateDb();
        var service = CreateService(db);

        await service.Invoking(s => s.RestoreSampleAsync(1, 2, maxCount))
            .Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithMessage("*between 1 and 25*");
    }

    [Fact]
    public async Task Test_RestoreSample_WhenQuotaExhausted_DoesNotCallYouTube()
    {
        using var db = CreateDb();
        SeedRestoreData(db);

        var quotaMock = new Mock<IQuotaTracker>();
        quotaMock.Setup(q => q.IsQuotaExhaustedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var ytMock = new Mock<IYouTubeApiClient>(MockBehavior.Strict);
        var service = CreateService(db, ytMock, quotaMock);

        await service.Invoking(s => s.RestoreSampleAsync(1, 2, 1))
            .Should().ThrowAsync<QuotaExhaustedException>();

        ytMock.Verify(y => y.AddVideoToPlaylistAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static PlaylistRestoreService CreateService(
        PlaylistMinerDbContext db,
        Mock<IYouTubeApiClient>? ytMock = null,
        Mock<IQuotaTracker>? quotaMock = null)
        => new(
            db,
            ytMock?.Object ?? Mock.Of<IYouTubeApiClient>(),
            quotaMock?.Object ?? Mock.Of<IQuotaTracker>(q => q.IsQuotaExhaustedAsync(It.IsAny<CancellationToken>()) == Task.FromResult(false)),
            NullLogger<PlaylistRestoreService>.Instance);

    private static void SeedRestoreData(PlaylistMinerDbContext db)
    {
        var now = DateTime.UtcNow;
        db.Playlists.AddRange(
            new Playlist { Id = 1, YouTubeId = "PLsource", Name = "Deleted AI Skills", CreatedAt = now, UpdatedAt = now, SyncedAt = now },
            new Playlist { Id = 2, YouTubeId = "PLtarget", Name = "AI skills", CreatedAt = now, UpdatedAt = now, SyncedAt = now });

        db.Videos.AddRange(
            new Video { Id = 1, YouTubeId = "yt-first", Title = "First", ChannelName = "Channel", ChannelId = "UC1", CreatedAt = now, UpdatedAt = now, SyncedAt = now },
            new Video { Id = 2, YouTubeId = "yt-second", Title = "Second", ChannelName = "Channel", ChannelId = "UC1", CreatedAt = now, UpdatedAt = now, SyncedAt = now },
            new Video { Id = 3, YouTubeId = "yt-third", Title = "Third", ChannelName = "Channel", ChannelId = "UC1", CreatedAt = now, UpdatedAt = now, SyncedAt = now });

        db.PlaylistVideos.AddRange(
            new PlaylistVideo { PlaylistId = 1, VideoId = 1, Position = 1, PlaylistItemId = "pli-source-first", AddedAt = now },
            new PlaylistVideo { PlaylistId = 1, VideoId = 2, Position = 2, PlaylistItemId = "pli-source-second", AddedAt = now },
            new PlaylistVideo { PlaylistId = 1, VideoId = 3, Position = 3, PlaylistItemId = "pli-source-third", AddedAt = now },
            new PlaylistVideo { PlaylistId = 2, VideoId = 2, Position = 10, PlaylistItemId = "pli-target-second", AddedAt = now });
        db.SaveChanges();
    }
}
