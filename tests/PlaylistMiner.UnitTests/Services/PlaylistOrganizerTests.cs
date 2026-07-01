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
public class PlaylistOrganizerTests
{
    private static PlaylistMinerDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<PlaylistMinerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlaylistMinerDbContext(opts);
    }

    private static (Video video, Playlist source, Playlist target) SeedBasicData(PlaylistMinerDbContext db)
    {
        var video = new Video
        {
            Id = 1,
            YouTubeId = "vid001",
            Title = "Test Video",
            ChannelName = "Channel",
            ChannelId = "UC1",
            Status = VideoStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow
        };

        var source = new Playlist
        {
            Id = 1,
            YouTubeId = "PLsource",
            Name = "Source Playlist",
            IsInbox = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow
        };

        var target = new Playlist
        {
            Id = 2,
            YouTubeId = "PLtarget",
            Name = "Target Playlist",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow
        };

        db.Videos.Add(video);
        db.Playlists.AddRange(source, target);
        db.PlaylistVideos.Add(new PlaylistVideo
        {
            PlaylistId = 1,
            VideoId = 1,
            Position = 0,
            PlaylistItemId = "PLI_source_item",
            AddedAt = DateTime.UtcNow
        });
        db.SaveChanges();

        return (video, source, target);
    }

    [Fact]
    public async Task Test_MoveVideo_AddsToTarget_RemovesFromSource()
    {
        // Arrange
        using var db = CreateDb();
        var (video, source, target) = SeedBasicData(db);
        var ytMock = new Mock<IYouTubeApiClient>();
        var organizer = new PlaylistOrganizer(db, ytMock.Object, NullLogger<PlaylistOrganizer>.Instance);

        // Act
        await organizer.MoveVideoAsync(1, 1, 2);

        // Assert
        ytMock.Verify(y => y.AddVideoToPlaylistAsync("PLtarget", "vid001", It.IsAny<CancellationToken>()), Times.Once);
        ytMock.Verify(y => y.RemoveVideoFromPlaylistAsync("PLsource", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        var sourcePv = await db.PlaylistVideos.FirstOrDefaultAsync(pv => pv.PlaylistId == 1 && pv.VideoId == 1);
        sourcePv.Should().BeNull();

        var targetPv = await db.PlaylistVideos.FirstOrDefaultAsync(pv => pv.PlaylistId == 2 && pv.VideoId == 1);
        targetPv.Should().NotBeNull();
    }

    [Fact]
    public async Task Test_MoveVideo_CreatesUndoLog()
    {
        // Arrange
        using var db = CreateDb();
        SeedBasicData(db);
        var ytMock = new Mock<IYouTubeApiClient>();
        var organizer = new PlaylistOrganizer(db, ytMock.Object, NullLogger<PlaylistOrganizer>.Instance);

        var before = DateTime.UtcNow;

        // Act
        await organizer.MoveVideoAsync(1, 1, 2);

        // Assert
        var undoLog = await db.UndoLogs.FirstOrDefaultAsync(ul => ul.VideoId == 1);
        undoLog.Should().NotBeNull();
        undoLog!.SourcePlaylistId.Should().Be(1);
        undoLog.TargetPlaylistId.Should().Be(2);
        undoLog.ExpiresAt.Should().BeCloseTo(before.AddDays(7), TimeSpan.FromSeconds(5));
        undoLog.Undone.Should().BeFalse();
    }

    [Fact]
    public async Task Test_UndoMove_ReversesAction_WithinWindow()
    {
        // Arrange
        using var db = CreateDb();
        var (video, source, target) = SeedBasicData(db);

        // Move video to target first
        db.PlaylistVideos.Remove(db.PlaylistVideos.First());
        db.PlaylistVideos.Add(new PlaylistVideo
        {
            PlaylistId = 2,
            VideoId = 1,
            Position = 0,
            AddedAt = DateTime.UtcNow
        });

        var undoLog = new UndoLog
        {
            VideoId = 1,
            Action = "Move",
            SourcePlaylistId = 1,
            TargetPlaylistId = 2,
            PerformedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            Undone = false
        };
        db.UndoLogs.Add(undoLog);
        await db.SaveChangesAsync();

        var ytMock = new Mock<IYouTubeApiClient>();
        var organizer = new PlaylistOrganizer(db, ytMock.Object, NullLogger<PlaylistOrganizer>.Instance);

        // Act
        await organizer.UndoMoveAsync(undoLog.Id);

        // Assert
        ytMock.Verify(y => y.AddVideoToPlaylistAsync("PLsource", "vid001", It.IsAny<CancellationToken>()), Times.Once);
        ytMock.Verify(y => y.RemoveVideoFromPlaylistAsync("PLtarget", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        var log = await db.UndoLogs.FindAsync(undoLog.Id);
        log!.Undone.Should().BeTrue();
    }

    [Fact]
    public async Task Test_UndoMove_Expired_ThrowsGoneException()
    {
        // Arrange
        using var db = CreateDb();
        SeedBasicData(db);

        var undoLog = new UndoLog
        {
            VideoId = 1,
            Action = "Move",
            SourcePlaylistId = 1,
            TargetPlaylistId = 2,
            PerformedAt = DateTime.UtcNow.AddDays(-8),
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            Undone = false
        };
        db.UndoLogs.Add(undoLog);
        await db.SaveChangesAsync();

        var ytMock = new Mock<IYouTubeApiClient>();
        var organizer = new PlaylistOrganizer(db, ytMock.Object, NullLogger<PlaylistOrganizer>.Instance);

        // Act & Assert
        await organizer.Invoking(o => o.UndoMoveAsync(undoLog.Id))
            .Should().ThrowAsync<GoneException>();
    }

    [Fact]
    public async Task Test_ConsolidatePlaylists_MergesOverlappingTopics()
    {
        // Arrange
        using var db = CreateDb();
        SeedBasicData(db);
        var ytMock = new Mock<IYouTubeApiClient>();
        var organizer = new PlaylistOrganizer(db, ytMock.Object, NullLogger<PlaylistOrganizer>.Instance);

        // Act
        var result = await organizer.ConsolidateAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Test_GetDuplicateReviewAsync_ReturnsVideosAssignedToMultiplePlaylists()
    {
        // Arrange
        using var db = CreateDb();
        var now = DateTime.UtcNow;

        var duplicateVideo = new Video
        {
            Id = 10,
            YouTubeId = "dupvideo01",
            Title = "Distributed Systems Deep Dive",
            Description = "Desc",
            ChannelName = "Channel",
            ChannelId = "UCdup1",
            ThumbnailUrl = "https://example.com/dup.jpg",
            Duration = TimeSpan.FromMinutes(10),
            PublishedAt = now.AddDays(-10),
            Status = VideoStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };

        var uniqueVideo = new Video
        {
            Id = 11,
            YouTubeId = "uniquevideo1",
            Title = "Unique Placement",
            Description = "Desc",
            ChannelName = "Channel",
            ChannelId = "UCdup2",
            ThumbnailUrl = "https://example.com/single.jpg",
            Duration = TimeSpan.FromMinutes(8),
            PublishedAt = now.AddDays(-5),
            Status = VideoStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };

        var managedA = new Playlist
        {
            Id = 21,
            YouTubeId = "PLmanagedA",
            Name = "AI Agents",
            IsManaged = true,
            Topic = "AI Agents",
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };

        var managedB = new Playlist
        {
            Id = 22,
            YouTubeId = "PLmanagedB",
            Name = "Backend Systems",
            IsManaged = true,
            Topic = "Backend Systems",
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };

        var unmanaged = new Playlist
        {
            Id = 23,
            YouTubeId = "PLmanual01",
            Name = "Watch Later Clone",
            IsManaged = false,
            CreatedAt = now,
            UpdatedAt = now,
            SyncedAt = now
        };

        db.Videos.AddRange(duplicateVideo, uniqueVideo);
        db.Playlists.AddRange(managedA, managedB, unmanaged);
        db.PlaylistVideos.AddRange(
            new PlaylistVideo { PlaylistId = managedA.Id, VideoId = duplicateVideo.Id, Position = 0, AddedAt = now },
            new PlaylistVideo { PlaylistId = managedB.Id, VideoId = duplicateVideo.Id, Position = 1, AddedAt = now },
            new PlaylistVideo { PlaylistId = unmanaged.Id, VideoId = duplicateVideo.Id, Position = 2, AddedAt = now },
            new PlaylistVideo { PlaylistId = managedA.Id, VideoId = uniqueVideo.Id, Position = 3, AddedAt = now });
        await db.SaveChangesAsync();

        var ytMock = new Mock<IYouTubeApiClient>();
        var organizer = new PlaylistOrganizer(db, ytMock.Object, NullLogger<PlaylistOrganizer>.Instance);

        // Act
        var result = await organizer.GetDuplicateReviewAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].VideoId.Should().Be(duplicateVideo.Id);
        result[0].YouTubeId.Should().Be("dupvideo01");
        result[0].PlaylistCount.Should().Be(3);
        result[0].Playlists.Should().BeEquivalentTo(
        [
            new DuplicatePlaylistDto(managedA.Id, managedA.Name, true, managedA.Topic),
            new DuplicatePlaylistDto(managedB.Id, managedB.Name, true, managedB.Topic),
            new DuplicatePlaylistDto(unmanaged.Id, unmanaged.Name, false, unmanaged.Topic)
        ]);
    }
}
