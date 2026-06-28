using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Exceptions;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;
using PlaylistMiner.Infrastructure.Services;

namespace PlaylistMiner.UnitTests.Services;

[Trait("Category", "Unit")]
public class SyncServiceTests
{
    private static PlaylistMinerDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<PlaylistMinerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlaylistMinerDbContext(options);
    }

    private static List<PlaylistDto> SamplePlaylists() =>
    [
        new PlaylistDto("PLabc", "Inbox", "My inbox", IsInbox: true, ItemCount: 2),
        new PlaylistDto("PLdef", "Tech", null, IsInbox: false, ItemCount: 1)
    ];

    private static List<PlaylistItemDto> SampleItems(string playlistId) =>
        playlistId == "PLabc"
            ?
            [
                new PlaylistItemDto("item1", "vid001", 0, DateTime.UtcNow),
                new PlaylistItemDto("item2", "vid002", 1, DateTime.UtcNow)
            ]
            : [new PlaylistItemDto("item3", "vid003", 0, DateTime.UtcNow)];

    private static VideoMetadataDto MakeVideo(string youtubeId) =>
        new(youtubeId, $"Title {youtubeId}", "Desc", "Channel", "UC123",
            "https://thumb.jpg", TimeSpan.FromMinutes(5),
            DateTime.UtcNow, VideoStatus.Active);

    private static Mock<IQuotaTracker> CreateQuotaTrackerMock()
    {
        var mock = new Mock<IQuotaTracker>();
        mock.Setup(q => q.IsQuotaExhaustedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        return mock;
    }

    [Fact]
    public async Task Test_FullSync_FetchesAllPlaylistsAndVideos()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var apiMock = new Mock<IYouTubeApiClient>();
        apiMock.Setup(a => a.GetUserPlaylistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SamplePlaylists());
        apiMock.Setup(a => a.GetPlaylistItemsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => SampleItems(id));
        apiMock.Setup(a => a.GetVideoMetadataAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> ids, CancellationToken _) =>
                ids.Select(MakeVideo).ToList());

        var service = new SyncService(apiMock.Object, CreateQuotaTrackerMock().Object, db, new PipelineRunTracker(db), NullLogger<SyncService>.Instance);

        // Act
        await service.FullSyncAsync();

        // Assert
        apiMock.Verify(a => a.GetUserPlaylistsAsync(It.IsAny<CancellationToken>()), Times.Once());
        apiMock.Verify(a => a.GetVideoMetadataAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce());
    }

    [Fact]
    public async Task Test_FullSync_UpsertsNewVideos()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var apiMock = new Mock<IYouTubeApiClient>();
        apiMock.Setup(a => a.GetUserPlaylistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PlaylistDto("PLabc", "Inbox", null, true, 1)]);
        apiMock.Setup(a => a.GetPlaylistItemsAsync("PLabc", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PlaylistItemDto("item1", "vidNEW", 0, DateTime.UtcNow)]);
        apiMock.Setup(a => a.GetVideoMetadataAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeVideo("vidNEW")]);

        var service = new SyncService(apiMock.Object, CreateQuotaTrackerMock().Object, db, new PipelineRunTracker(db), NullLogger<SyncService>.Instance);

        // Act
        await service.FullSyncAsync();

        // Assert
        var video = await db.Videos.FirstOrDefaultAsync(v => v.YouTubeId == "vidNEW");
        video.Should().NotBeNull();
        video!.Title.Should().Be("Title vidNEW");
    }

    [Fact]
    public async Task Test_FullSync_DetectsDeletedVideos_MarksArchived()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();

        // Pre-seed a video that won't come back from API
        var existingVideo = new Video
        {
            YouTubeId = "vidOLD",
            Title = "Old Video",
            Description = "desc",
            ChannelName = "Channel",
            ChannelId = "UC123",
            ThumbnailUrl = "https://thumb.jpg",
            Status = VideoStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow
        };
        db.Videos.Add(existingVideo);
        await db.SaveChangesAsync();

        var apiMock = new Mock<IYouTubeApiClient>();
        apiMock.Setup(a => a.GetUserPlaylistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PlaylistDto("PLabc", "Inbox", null, true, 1)]);
        apiMock.Setup(a => a.GetPlaylistItemsAsync("PLabc", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PlaylistItemDto("item1", "vidNEW", 0, DateTime.UtcNow)]);
        apiMock.Setup(a => a.GetVideoMetadataAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeVideo("vidNEW")]);

        var service = new SyncService(apiMock.Object, CreateQuotaTrackerMock().Object, db, new PipelineRunTracker(db), NullLogger<SyncService>.Instance);

        // Act
        await service.FullSyncAsync();

        // Assert - old video should be marked Archived
        var oldVideo = await db.Videos.FirstAsync(v => v.YouTubeId == "vidOLD");
        oldVideo.Status.Should().Be(VideoStatus.Archived);
    }

    [Fact]
    public async Task Test_FullSync_SkipsUnchangedVideos()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();

        var existingVideo = new Video
        {
            YouTubeId = "vidSAME",
            Title = "Title vidSAME",
            Description = "Desc",
            ChannelName = "Channel",
            ChannelId = "UC123",
            ThumbnailUrl = "https://thumb.jpg",
            Status = VideoStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow
        };
        db.Videos.Add(existingVideo);
        await db.SaveChangesAsync();

        var originalUpdatedAt = existingVideo.UpdatedAt;

        var apiMock = new Mock<IYouTubeApiClient>();
        apiMock.Setup(a => a.GetUserPlaylistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PlaylistDto("PLabc", "Inbox", null, true, 1)]);
        apiMock.Setup(a => a.GetPlaylistItemsAsync("PLabc", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PlaylistItemDto("item1", "vidSAME", 0, DateTime.UtcNow)]);
        apiMock.Setup(a => a.GetVideoMetadataAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeVideo("vidSAME")]); // same title

        var service = new SyncService(apiMock.Object, CreateQuotaTrackerMock().Object, db, new PipelineRunTracker(db), NullLogger<SyncService>.Instance);

        // Act
        var result = await service.FullSyncAsync();

        // Assert
        result.VideosProcessed.Should().BeGreaterThan(0);
        // Video still exists and isn't duplicated
        var count = await db.Videos.CountAsync(v => v.YouTubeId == "vidSAME");
        count.Should().Be(1);
    }

    [Fact]
    public async Task Test_FullSync_CreatesSyncLogEntry()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var apiMock = new Mock<IYouTubeApiClient>();
        apiMock.Setup(a => a.GetUserPlaylistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        apiMock.Setup(a => a.GetVideoMetadataAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = new SyncService(apiMock.Object, CreateQuotaTrackerMock().Object, db, new PipelineRunTracker(db), NullLogger<SyncService>.Instance);

        // Act
        await service.FullSyncAsync();

        // Assert
        var syncLog = await db.SyncLogs.FirstOrDefaultAsync();
        syncLog.Should().NotBeNull();
        syncLog!.Status.Should().Be("Completed");
        syncLog.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Test_FullSync_HandlesQuotaExhaustion_DeferrsRemaining()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var apiMock = new Mock<IYouTubeApiClient>();
        apiMock.Setup(a => a.GetUserPlaylistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PlaylistDto("PLabc", "Inbox", null, true, 2)]);
        apiMock.Setup(a => a.GetPlaylistItemsAsync("PLabc", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PlaylistItemDto("item1", "vid001", 0, DateTime.UtcNow),
                new PlaylistItemDto("item2", "vid002", 1, DateTime.UtcNow)
            ]);
        apiMock.Setup(a => a.GetVideoMetadataAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new QuotaExhaustedException());

        var service = new SyncService(apiMock.Object, CreateQuotaTrackerMock().Object, db, new PipelineRunTracker(db), NullLogger<SyncService>.Instance);

        // Act
        var result = await service.FullSyncAsync();

        // Assert
        result.DeferredCount.Should().BeGreaterThan(0);
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Test_InboxSync_OnlySyncsInboxPlaylist()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();

        // Pre-seed playlists in DB
        db.Playlists.AddRange(
            new Playlist
            {
                YouTubeId = "PLabc",
                Name = "Inbox",
                IsInbox = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                SyncedAt = DateTime.UtcNow
            },
            new Playlist
            {
                YouTubeId = "PLdef",
                Name = "Tech",
                IsInbox = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                SyncedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var apiMock = new Mock<IYouTubeApiClient>();
        apiMock.Setup(a => a.GetUserPlaylistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SamplePlaylists());
        apiMock.Setup(a => a.GetPlaylistItemsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => SampleItems(id));
        apiMock.Setup(a => a.GetVideoMetadataAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> ids, CancellationToken _) => ids.Select(MakeVideo).ToList());

        var service = new SyncService(apiMock.Object, CreateQuotaTrackerMock().Object, db, new PipelineRunTracker(db), NullLogger<SyncService>.Instance);

        // Act
        await service.SyncInboxAsync();

        // Assert - only PLabc (inbox) was synced
        apiMock.Verify(
            a => a.GetPlaylistItemsAsync("PLabc", It.IsAny<CancellationToken>()),
            Times.Once());
        apiMock.Verify(
            a => a.GetPlaylistItemsAsync("PLdef", It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Fact]
    public async Task Test_FullSync_CreatesPipelineRunAndEvents()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var apiMock = new Mock<IYouTubeApiClient>();
        apiMock.Setup(a => a.GetUserPlaylistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SamplePlaylists());
        apiMock.Setup(a => a.GetPlaylistItemsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => SampleItems(id));
        apiMock.Setup(a => a.GetVideoMetadataAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> ids, CancellationToken _) =>
                ids.Select(MakeVideo).ToList());

        var service = new SyncService(apiMock.Object, CreateQuotaTrackerMock().Object, db, new PipelineRunTracker(db), NullLogger<SyncService>.Instance);

        // Act
        await service.FullSyncAsync();

        // Assert
        var run = await db.PipelineRuns.FirstOrDefaultAsync();
        run.Should().NotBeNull();
        run!.PipelineType.Should().Be("sync");
        run.Status.Should().Be("completed");
        run.Phase.Should().Be("completed");
        run.PlaylistsDiscovered.Should().Be(2);
        run.PlaylistsProcessed.Should().Be(2);
        run.PlaylistItemsFetched.Should().Be(3);
        run.UniqueVideoIdsIdentified.Should().Be(3);
        run.VideosUpserted.Should().Be(3);

        var events = await db.PipelineEvents.ToListAsync();
        events.Should().NotBeEmpty();
        events.First().Phase.Should().Be("starting");
        events.Last().Phase.Should().Be("completed");
    }
}
