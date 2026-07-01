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
public class RemoteDuplicateCleanupServiceTests
{
    private static PlaylistMinerDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<PlaylistMinerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlaylistMinerDbContext(opts);
    }

    [Fact]
    public async Task Test_BuildPlan_WhenVideoExistsInTwoPlaylists_KeepsWinningPlaylistAndPlansOneRemoval()
    {
        // Arrange
        using var db = CreateDb();
        var now = DateTime.UtcNow;
        var inbox = new Playlist
        {
            Id = 1,
            YouTubeId = "PLinbox",
            Name = "Inbox",
            IsInbox = true,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var topic = new Playlist
        {
            Id = 2,
            YouTubeId = "PLtopic",
            Name = "Distributed Systems",
            IsInbox = false,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var video = new Video
        {
            Id = 10,
            YouTubeId = "vid001",
            Title = "Distributed Systems Deep Dive",
            Description = "desc",
            ChannelName = "Channel",
            ChannelId = "UC1",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            Duration = TimeSpan.FromMinutes(10),
            PublishedAt = now,
            Status = VideoStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };

        db.Playlists.AddRange(inbox, topic);
        db.Videos.Add(video);
        db.PlaylistVideos.AddRange(
            new PlaylistVideo { PlaylistId = inbox.Id, VideoId = video.Id, PlaylistItemId = "pli-inbox", Position = 0, AddedAt = now },
            new PlaylistVideo { PlaylistId = topic.Id, VideoId = video.Id, PlaylistItemId = "pli-topic", Position = 0, AddedAt = now });
        await db.SaveChangesAsync();

        var ytMock = new Mock<IYouTubeApiClient>();
        ytMock.Setup(y => y.GetUserPlaylistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PlaylistDto("PLinbox", "Inbox", null, true, 1, inbox.Id),
                new PlaylistDto("PLtopic", "Distributed Systems", null, false, 1, topic.Id)
            ]);
        ytMock.Setup(y => y.GetPlaylistItemsAsync("PLinbox", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PlaylistItemDto("pli-inbox", "vid001", 0, now)
            ]);
        ytMock.Setup(y => y.GetPlaylistItemsAsync("PLtopic", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PlaylistItemDto("pli-topic", "vid001", 0, now)
            ]);
        var quotaMock = new Mock<IQuotaTracker>();
        IRemoteDuplicateCleanupService service = new RemoteDuplicateCleanupService(
            db,
            ytMock.Object,
            quotaMock.Object,
            new PipelineRunTracker(db),
            NullLogger<RemoteDuplicateCleanupService>.Instance);

        // Act
        var plan = await service.BuildPlanAsync();

        // Assert
        plan.Should().HaveCount(1);
        plan[0].VideoId.Should().Be(video.Id);
        plan[0].WinnerPlaylistId.Should().Be(topic.Id);
        plan[0].WinnerPlaylistName.Should().Be(topic.Name);
        plan[0].LoserPlaylists.Should().ContainSingle();
        plan[0].LoserPlaylists[0].PlaylistId.Should().Be(inbox.Id);
        plan[0].LoserPlaylists[0].PlaylistItemId.Should().Be("pli-inbox");
    }

    [Fact]
    public async Task Test_BuildPlan_WhenLocalStateAlreadyDeduped_DetectsRemoteDuplicateMemberships()
    {
        // Arrange
        using var db = CreateDb();
        var now = DateTime.UtcNow;
        var inbox = new Playlist
        {
            Id = 1,
            YouTubeId = "PLinbox",
            Name = "Inbox",
            IsInbox = true,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var topic = new Playlist
        {
            Id = 2,
            YouTubeId = "PLtopic",
            Name = "Distributed Systems",
            IsInbox = false,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var video = new Video
        {
            Id = 10,
            YouTubeId = "vid001",
            Title = "Distributed Systems Deep Dive",
            Description = "desc",
            ChannelName = "Channel",
            ChannelId = "UC1",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            Duration = TimeSpan.FromMinutes(10),
            PublishedAt = now,
            Status = VideoStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };

        db.Playlists.AddRange(inbox, topic);
        db.Videos.Add(video);
        db.PlaylistVideos.Add(new PlaylistVideo
        {
            PlaylistId = topic.Id,
            VideoId = video.Id,
            PlaylistItemId = "pli-topic",
            Position = 0,
            AddedAt = now
        });
        await db.SaveChangesAsync();

        var ytMock = new Mock<IYouTubeApiClient>();
        ytMock.Setup(y => y.GetUserPlaylistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PlaylistDto("PLinbox", "Inbox", null, true, 1, inbox.Id),
                new PlaylistDto("PLtopic", "Distributed Systems", null, false, 1, topic.Id)
            ]);
        ytMock.Setup(y => y.GetPlaylistItemsAsync("PLinbox", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PlaylistItemDto("pli-inbox", "vid001", 0, now)
            ]);
        ytMock.Setup(y => y.GetPlaylistItemsAsync("PLtopic", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PlaylistItemDto("pli-topic", "vid001", 0, now)
            ]);

        var quotaMock = new Mock<IQuotaTracker>();
        IRemoteDuplicateCleanupService service = new RemoteDuplicateCleanupService(
            db,
            ytMock.Object,
            quotaMock.Object,
            new PipelineRunTracker(db),
            NullLogger<RemoteDuplicateCleanupService>.Instance);

        // Act
        var plan = await service.BuildPlanAsync();

        // Assert
        plan.Should().HaveCount(1);
        plan[0].WinnerPlaylistId.Should().Be(topic.Id);
        plan[0].LoserPlaylists.Should().ContainSingle();
        plan[0].LoserPlaylists[0].PlaylistId.Should().Be(inbox.Id);
        plan[0].LoserPlaylists[0].PlaylistItemId.Should().Be("pli-inbox");
    }

    [Fact]
    public async Task Test_BuildPlan_WhenPlaylistItemIdMissing_FlagsItemAsUnresolved()
    {
        // Arrange
        using var db = CreateDb();
        var now = DateTime.UtcNow;
        var a = new Playlist
        {
            Id = 1,
            YouTubeId = "PLa",
            Name = "Playlist A",
            IsInbox = true,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var b = new Playlist
        {
            Id = 2,
            YouTubeId = "PLb",
            Name = "Playlist B",
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var video = new Video
        {
            Id = 10,
            YouTubeId = "vid001",
            Title = "Video",
            Description = "desc",
            ChannelName = "Channel",
            ChannelId = "UC1",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            Duration = TimeSpan.FromMinutes(10),
            PublishedAt = now,
            Status = VideoStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };

        db.Playlists.AddRange(a, b);
        db.Videos.Add(video);
        db.PlaylistVideos.AddRange(
            new PlaylistVideo { PlaylistId = a.Id, VideoId = video.Id, PlaylistItemId = null, Position = 0, AddedAt = now },
            new PlaylistVideo { PlaylistId = b.Id, VideoId = video.Id, PlaylistItemId = "pli-b", Position = 0, AddedAt = now });
        await db.SaveChangesAsync();

        var ytMock = new Mock<IYouTubeApiClient>();
        ytMock.Setup(y => y.GetUserPlaylistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PlaylistDto("PLa", "Playlist A", null, true, 1, a.Id),
                new PlaylistDto("PLb", "Playlist B", null, false, 1, b.Id)
            ]);
        ytMock.Setup(y => y.GetPlaylistItemsAsync("PLa", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PlaylistItemDto(null!, "vid001", 0, now)
            ]);
        ytMock.Setup(y => y.GetPlaylistItemsAsync("PLb", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PlaylistItemDto("pli-b", "vid001", 0, now)
            ]);
        var quotaMock = new Mock<IQuotaTracker>();
        IRemoteDuplicateCleanupService service = new RemoteDuplicateCleanupService(
            db,
            ytMock.Object,
            quotaMock.Object,
            new PipelineRunTracker(db),
            NullLogger<RemoteDuplicateCleanupService>.Instance);

        // Act
        var plan = await service.BuildPlanAsync();

        // Assert
        plan.Should().HaveCount(1);
        plan[0].HasUnresolvedRemovals.Should().BeTrue();
        plan[0].LoserPlaylists.Should().ContainSingle();
        plan[0].LoserPlaylists[0].PlaylistItemId.Should().BeNull();
    }

    [Fact]
    public async Task Test_BuildPlan_WhenPlaylistItemIdMissing_UsesRemotePlaylistItemIdWithoutMutatingLocalLink()
    {
        // Arrange
        using var db = CreateDb();
        var now = DateTime.UtcNow;
        var loser = new Playlist
        {
            Id = 1,
            YouTubeId = "PLloser",
            Name = "Inbox",
            IsInbox = true,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var winner = new Playlist
        {
            Id = 2,
            YouTubeId = "PLwinner",
            Name = "Backend Systems",
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var video = new Video
        {
            Id = 10,
            YouTubeId = "vid001",
            Title = "Video",
            Description = "desc",
            ChannelName = "Channel",
            ChannelId = "UC1",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            Duration = TimeSpan.FromMinutes(10),
            PublishedAt = now,
            Status = VideoStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };

        db.Playlists.AddRange(loser, winner);
        db.Videos.Add(video);
        db.PlaylistVideos.AddRange(
            new PlaylistVideo { PlaylistId = loser.Id, VideoId = video.Id, PlaylistItemId = null, Position = 0, AddedAt = now },
            new PlaylistVideo { PlaylistId = winner.Id, VideoId = video.Id, PlaylistItemId = "pli-winner", Position = 0, AddedAt = now });
        await db.SaveChangesAsync();

        var ytMock = new Mock<IYouTubeApiClient>();
        ytMock.Setup(y => y.GetUserPlaylistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PlaylistDto("PLloser", "Inbox", null, true, 1, loser.Id),
                new PlaylistDto("PLwinner", "Backend Systems", null, false, 1, winner.Id)
            ]);
        ytMock.Setup(y => y.GetPlaylistItemsAsync("PLloser", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PlaylistItemDto("pli-loser", "vid001", 0, now),
                new PlaylistItemDto("pli-other", "vid999", 1, now)
            ]);
        ytMock.Setup(y => y.GetPlaylistItemsAsync("PLwinner", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PlaylistItemDto("pli-winner", "vid001", 0, now)
            ]);
        var quotaMock = new Mock<IQuotaTracker>();
        IRemoteDuplicateCleanupService service = new RemoteDuplicateCleanupService(
            db,
            ytMock.Object,
            quotaMock.Object,
            new PipelineRunTracker(db),
            NullLogger<RemoteDuplicateCleanupService>.Instance);

        // Act
        var plan = await service.BuildPlanAsync();

        // Assert
        plan.Should().HaveCount(1);
        plan[0].HasUnresolvedRemovals.Should().BeFalse();
        plan[0].LoserPlaylists.Should().ContainSingle();
        plan[0].LoserPlaylists[0].PlaylistItemId.Should().Be("pli-loser");
        (await db.PlaylistVideos.SingleAsync(pv => pv.PlaylistId == loser.Id && pv.VideoId == video.Id)).PlaylistItemId.Should().BeNull();
        ytMock.Verify(y => y.GetPlaylistItemsAsync("PLloser", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Test_BuildPlan_WhenMultipleVideosNeedSamePlaylistEnumeration_FetchesPlaylistOnce()
    {
        // Arrange
        using var db = CreateDb();
        var now = DateTime.UtcNow;
        var loser = new Playlist
        {
            Id = 1,
            YouTubeId = "PLloser",
            Name = "Inbox",
            IsInbox = true,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var winnerA = new Playlist
        {
            Id = 2,
            YouTubeId = "PLwinnerA",
            Name = "Winner A",
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var winnerB = new Playlist
        {
            Id = 3,
            YouTubeId = "PLwinnerB",
            Name = "Winner B",
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var videoA = new Video
        {
            Id = 10,
            YouTubeId = "vid001",
            Title = "Video A",
            Description = "desc",
            ChannelName = "Channel",
            ChannelId = "UC1",
            ThumbnailUrl = "https://example.com/thumb-a.jpg",
            Duration = TimeSpan.FromMinutes(10),
            PublishedAt = now,
            Status = VideoStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var videoB = new Video
        {
            Id = 11,
            YouTubeId = "vid002",
            Title = "Video B",
            Description = "desc",
            ChannelName = "Channel",
            ChannelId = "UC2",
            ThumbnailUrl = "https://example.com/thumb-b.jpg",
            Duration = TimeSpan.FromMinutes(11),
            PublishedAt = now,
            Status = VideoStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };

        db.Playlists.AddRange(loser, winnerA, winnerB);
        db.Videos.AddRange(videoA, videoB);
        db.PlaylistVideos.AddRange(
            new PlaylistVideo { PlaylistId = loser.Id, VideoId = videoA.Id, PlaylistItemId = null, Position = 0, AddedAt = now },
            new PlaylistVideo { PlaylistId = winnerA.Id, VideoId = videoA.Id, PlaylistItemId = "pli-winner-a", Position = 0, AddedAt = now },
            new PlaylistVideo { PlaylistId = loser.Id, VideoId = videoB.Id, PlaylistItemId = null, Position = 1, AddedAt = now },
            new PlaylistVideo { PlaylistId = winnerB.Id, VideoId = videoB.Id, PlaylistItemId = "pli-winner-b", Position = 0, AddedAt = now });
        await db.SaveChangesAsync();

        var ytMock = new Mock<IYouTubeApiClient>();
        ytMock.Setup(y => y.GetUserPlaylistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PlaylistDto("PLloser", "Inbox", null, true, 2, loser.Id),
                new PlaylistDto("PLwinnerA", "Winner A", null, false, 1, winnerA.Id),
                new PlaylistDto("PLwinnerB", "Winner B", null, false, 1, winnerB.Id)
            ]);
        ytMock.Setup(y => y.GetPlaylistItemsAsync("PLloser", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PlaylistItemDto("pli-loser-a", "vid001", 0, now),
                new PlaylistItemDto("pli-loser-b", "vid002", 1, now)
            ]);
        ytMock.Setup(y => y.GetPlaylistItemsAsync("PLwinnerA", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PlaylistItemDto("pli-winner-a", "vid001", 0, now)
            ]);
        ytMock.Setup(y => y.GetPlaylistItemsAsync("PLwinnerB", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PlaylistItemDto("pli-winner-b", "vid002", 0, now)
            ]);
        var quotaMock = new Mock<IQuotaTracker>();
        IRemoteDuplicateCleanupService service = new RemoteDuplicateCleanupService(
            db,
            ytMock.Object,
            quotaMock.Object,
            new PipelineRunTracker(db),
            NullLogger<RemoteDuplicateCleanupService>.Instance);

        // Act
        var plan = await service.BuildPlanAsync();

        // Assert
        plan.Should().HaveCount(2);
        plan.Should().OnlyContain(item => item.HasUnresolvedRemovals == false);
        (await db.PlaylistVideos.Where(pv => pv.PlaylistId == loser.Id).OrderBy(pv => pv.VideoId).Select(pv => pv.PlaylistItemId).ToListAsync())
            .Should()
            .Equal([null, null]);
        ytMock.Verify(y => y.GetPlaylistItemsAsync("PLloser", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Test_Execute_RemovesLoserPlaylistMembershipsOnYouTube_AndLocalLink()
    {
        // Arrange
        using var db = CreateDb();
        var now = DateTime.UtcNow;
        var loser = new Playlist
        {
            Id = 1,
            YouTubeId = "PLloser",
            Name = "Inbox",
            IsInbox = true,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var winner = new Playlist
        {
            Id = 2,
            YouTubeId = "PLwinner",
            Name = "Backend Systems",
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var video = new Video
        {
            Id = 10,
            YouTubeId = "vid001",
            Title = "Video",
            Description = "desc",
            ChannelName = "Channel",
            ChannelId = "UC1",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            Duration = TimeSpan.FromMinutes(10),
            PublishedAt = now,
            Status = VideoStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };

        db.Playlists.AddRange(loser, winner);
        db.Videos.Add(video);
        db.PlaylistVideos.AddRange(
            new PlaylistVideo { PlaylistId = loser.Id, VideoId = video.Id, PlaylistItemId = "pli-loser", Position = 0, AddedAt = now },
            new PlaylistVideo { PlaylistId = winner.Id, VideoId = video.Id, PlaylistItemId = "pli-winner", Position = 0, AddedAt = now });
        await db.SaveChangesAsync();

        var ytMock = new Mock<IYouTubeApiClient>();
        var quotaMock = new Mock<IQuotaTracker>();
        quotaMock.Setup(q => q.IsQuotaExhaustedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        IRemoteDuplicateCleanupService service = new RemoteDuplicateCleanupService(
            db,
            ytMock.Object,
            quotaMock.Object,
            new PipelineRunTracker(db),
            NullLogger<RemoteDuplicateCleanupService>.Instance);

        var plan = new List<RemoteDuplicateCleanupItemDto>
        {
            new(
                video.Id,
                video.YouTubeId,
                video.Title,
                winner.Id,
                winner.Name,
                false,
                [new RemoteDuplicateRemovalTargetDto(loser.Id, loser.Name, "pli-loser")])
        };

        // Act
        var result = await service.ExecuteAsync(plan);

        // Assert
        ytMock.Verify(y => y.RemoveVideoFromPlaylistAsync("PLloser", "pli-loser", It.IsAny<CancellationToken>()), Times.Once);
        result.RemovalsExecuted.Should().Be(1);
        result.DeferredCount.Should().Be(0);
        (await db.PlaylistVideos.Where(pv => pv.VideoId == video.Id).ToListAsync()).Should().ContainSingle(pv => pv.PlaylistId == winner.Id);
    }

    [Fact]
    public async Task Test_Execute_WhenRemovalFails_LeavesLocalLink_AndReturnsError()
    {
        // Arrange
        using var db = CreateDb();
        var now = DateTime.UtcNow;
        var loser = new Playlist
        {
            Id = 1,
            YouTubeId = "PLloser",
            Name = "Inbox",
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var winner = new Playlist
        {
            Id = 2,
            YouTubeId = "PLwinner",
            Name = "Backend Systems",
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var video = new Video
        {
            Id = 10,
            YouTubeId = "vid001",
            Title = "Video",
            Description = "desc",
            ChannelName = "Channel",
            ChannelId = "UC1",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            Duration = TimeSpan.FromMinutes(10),
            PublishedAt = now,
            Status = VideoStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };

        db.Playlists.AddRange(loser, winner);
        db.Videos.Add(video);
        db.PlaylistVideos.AddRange(
            new PlaylistVideo { PlaylistId = loser.Id, VideoId = video.Id, PlaylistItemId = "pli-loser", Position = 0, AddedAt = now },
            new PlaylistVideo { PlaylistId = winner.Id, VideoId = video.Id, PlaylistItemId = "pli-winner", Position = 0, AddedAt = now });
        await db.SaveChangesAsync();

        var ytMock = new Mock<IYouTubeApiClient>();
        ytMock.Setup(y => y.RemoveVideoFromPlaylistAsync("PLloser", "pli-loser", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var quotaMock = new Mock<IQuotaTracker>();
        quotaMock.Setup(q => q.IsQuotaExhaustedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        IRemoteDuplicateCleanupService service = new RemoteDuplicateCleanupService(
            db,
            ytMock.Object,
            quotaMock.Object,
            new PipelineRunTracker(db),
            NullLogger<RemoteDuplicateCleanupService>.Instance);

        var plan = new List<RemoteDuplicateCleanupItemDto>
        {
            new(
                video.Id,
                video.YouTubeId,
                video.Title,
                winner.Id,
                winner.Name,
                false,
                [new RemoteDuplicateRemovalTargetDto(loser.Id, loser.Name, "pli-loser")])
        };

        // Act
        var result = await service.ExecuteAsync(plan);

        // Assert
        result.RemovalsExecuted.Should().Be(0);
        result.Errors.Should().ContainSingle();
        (await db.PlaylistVideos.Where(pv => pv.VideoId == video.Id).ToListAsync()).Should().HaveCount(2);
    }

    [Fact]
    public async Task Test_Execute_WhenQuotaExhausted_DefersRemaining()
    {
        // Arrange
        using var db = CreateDb();
        var ytMock = new Mock<IYouTubeApiClient>(MockBehavior.Strict);
        var quotaMock = new Mock<IQuotaTracker>();
        quotaMock.Setup(q => q.IsQuotaExhaustedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        IRemoteDuplicateCleanupService service = new RemoteDuplicateCleanupService(
            db,
            ytMock.Object,
            quotaMock.Object,
            new PipelineRunTracker(db),
            NullLogger<RemoteDuplicateCleanupService>.Instance);

        var plan = new List<RemoteDuplicateCleanupItemDto>
        {
            new(
                10,
                "vid001",
                "Video",
                2,
                "Winner",
                false,
                [new RemoteDuplicateRemovalTargetDto(1, "Loser", "pli-loser")])
        };

        // Act
        var result = await service.ExecuteAsync(plan);

        // Assert
        result.RemovalsExecuted.Should().Be(0);
        result.DeferredCount.Should().Be(1);
        result.Errors.Should().ContainSingle(e => e.Contains("quota", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Test_Execute_WhenWinnerLinkNoLongerExists_SkipsWithoutRemoteDelete()
    {
        // Arrange
        using var db = CreateDb();
        var now = DateTime.UtcNow;
        var loser = new Playlist
        {
            Id = 1,
            YouTubeId = "PLloser",
            Name = "Inbox",
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var video = new Video
        {
            Id = 10,
            YouTubeId = "vid001",
            Title = "Video",
            Description = "desc",
            ChannelName = "Channel",
            ChannelId = "UC1",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            Duration = TimeSpan.FromMinutes(10),
            PublishedAt = now,
            Status = VideoStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };

        db.Playlists.Add(loser);
        db.Videos.Add(video);
        db.PlaylistVideos.Add(new PlaylistVideo { PlaylistId = loser.Id, VideoId = video.Id, PlaylistItemId = "pli-loser", Position = 0, AddedAt = now });
        await db.SaveChangesAsync();

        var ytMock = new Mock<IYouTubeApiClient>(MockBehavior.Strict);
        var quotaMock = new Mock<IQuotaTracker>();
        quotaMock.Setup(q => q.IsQuotaExhaustedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        IRemoteDuplicateCleanupService service = new RemoteDuplicateCleanupService(
            db,
            ytMock.Object,
            quotaMock.Object,
            new PipelineRunTracker(db),
            NullLogger<RemoteDuplicateCleanupService>.Instance);

        var plan = new List<RemoteDuplicateCleanupItemDto>
        {
            new(
                video.Id,
                video.YouTubeId,
                video.Title,
                2,
                "Winner",
                false,
                [new RemoteDuplicateRemovalTargetDto(loser.Id, loser.Name, "pli-loser")])
        };

        // Act
        var result = await service.ExecuteAsync(plan);

        // Assert
        result.RemovalsExecuted.Should().Be(0);
        result.RemovalsSkipped.Should().Be(1);
        result.Errors.Should().ContainSingle(e => e.Contains("winner", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Test_Execute_WhenLoserLinkAlreadyGone_SkipsWithoutRemoteDelete()
    {
        // Arrange
        using var db = CreateDb();
        var now = DateTime.UtcNow;
        var loser = new Playlist
        {
            Id = 1,
            YouTubeId = "PLloser",
            Name = "Loser",
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var winner = new Playlist
        {
            Id = 2,
            YouTubeId = "PLwinner",
            Name = "Winner",
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var video = new Video
        {
            Id = 10,
            YouTubeId = "vid001",
            Title = "Video",
            Description = "desc",
            ChannelName = "Channel",
            ChannelId = "UC1",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            Duration = TimeSpan.FromMinutes(10),
            PublishedAt = now,
            Status = VideoStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };

        db.Playlists.AddRange(loser, winner);
        db.Videos.Add(video);
        db.PlaylistVideos.Add(new PlaylistVideo { PlaylistId = winner.Id, VideoId = video.Id, PlaylistItemId = "pli-winner", Position = 0, AddedAt = now });
        await db.SaveChangesAsync();

        var ytMock = new Mock<IYouTubeApiClient>(MockBehavior.Strict);
        var quotaMock = new Mock<IQuotaTracker>();
        quotaMock.Setup(q => q.IsQuotaExhaustedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        IRemoteDuplicateCleanupService service = new RemoteDuplicateCleanupService(
            db,
            ytMock.Object,
            quotaMock.Object,
            new PipelineRunTracker(db),
            NullLogger<RemoteDuplicateCleanupService>.Instance);

        var plan = new List<RemoteDuplicateCleanupItemDto>
        {
            new(
                video.Id,
                video.YouTubeId,
                video.Title,
                winner.Id,
                winner.Name,
                false,
                [new RemoteDuplicateRemovalTargetDto(1, "Loser", "pli-loser")])
        };

        // Act
        var result = await service.ExecuteAsync(plan);

        // Assert
        result.RemovalsExecuted.Should().Be(0);
        result.RemovalsSkipped.Should().Be(1);
        result.Errors.Should().ContainSingle(e => e.Contains("already gone", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Test_Execute_WhenLocalPlaylistItemIdChanged_UsesCurrentLocalValue()
    {
        // Arrange
        using var db = CreateDb();
        var now = DateTime.UtcNow;
        var loser = new Playlist
        {
            Id = 1,
            YouTubeId = "PLloser",
            Name = "Loser",
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var winner = new Playlist
        {
            Id = 2,
            YouTubeId = "PLwinner",
            Name = "Winner",
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };
        var video = new Video
        {
            Id = 10,
            YouTubeId = "vid001",
            Title = "Video",
            Description = "desc",
            ChannelName = "Channel",
            ChannelId = "UC1",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            Duration = TimeSpan.FromMinutes(10),
            PublishedAt = now,
            Status = VideoStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };

        db.Playlists.AddRange(loser, winner);
        db.Videos.Add(video);
        db.PlaylistVideos.AddRange(
            new PlaylistVideo { PlaylistId = loser.Id, VideoId = video.Id, PlaylistItemId = "pli-current", Position = 0, AddedAt = now },
            new PlaylistVideo { PlaylistId = winner.Id, VideoId = video.Id, PlaylistItemId = "pli-winner", Position = 0, AddedAt = now });
        await db.SaveChangesAsync();

        var ytMock = new Mock<IYouTubeApiClient>();
        var quotaMock = new Mock<IQuotaTracker>();
        quotaMock.Setup(q => q.IsQuotaExhaustedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        IRemoteDuplicateCleanupService service = new RemoteDuplicateCleanupService(
            db,
            ytMock.Object,
            quotaMock.Object,
            new PipelineRunTracker(db),
            NullLogger<RemoteDuplicateCleanupService>.Instance);

        var plan = new List<RemoteDuplicateCleanupItemDto>
        {
            new(
                video.Id,
                video.YouTubeId,
                video.Title,
                winner.Id,
                winner.Name,
                false,
                [new RemoteDuplicateRemovalTargetDto(loser.Id, loser.Name, "pli-stale")])
        };

        // Act
        var result = await service.ExecuteAsync(plan);

        // Assert
        ytMock.Verify(y => y.RemoveVideoFromPlaylistAsync("PLloser", "pli-current", It.IsAny<CancellationToken>()), Times.Once);
        result.RemovalsExecuted.Should().Be(1);
    }
}
