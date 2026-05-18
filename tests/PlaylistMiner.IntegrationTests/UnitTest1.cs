using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.IntegrationTests;

public class DbContextSmokeTests
{
    private static PlaylistMinerDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<PlaylistMinerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new PlaylistMinerDbContext(options);
    }

    [Fact]
    public async Task CanInsertAndQueryVideo()
    {
        await using var context = CreateInMemoryContext();

        var video = new Video
        {
            YouTubeId = "dQw4w9WgXcQ",
            Title = "Test Video",
            Description = "A test video",
            ChannelName = "TestChannel",
            ChannelId = "UC123",
            ThumbnailUrl = "https://img.youtube.com/vi/dQw4w9WgXcQ/0.jpg",
            Duration = TimeSpan.FromMinutes(3),
            PublishedAt = DateTime.UtcNow,
            Status = VideoStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow
        };

        context.Videos.Add(video);
        await context.SaveChangesAsync();

        var found = await context.Videos.FirstOrDefaultAsync(v => v.YouTubeId == "dQw4w9WgXcQ");
        found.Should().NotBeNull();
        found!.Title.Should().Be("Test Video");
    }

    [Fact]
    public async Task CanInsertTagWithVideoTag()
    {
        await using var context = CreateInMemoryContext();

        var video = new Video
        {
            YouTubeId = "abc123",
            Title = "C# Tutorial",
            Description = "Learn C#",
            ChannelName = "DevChannel",
            ChannelId = "UC456",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            Duration = TimeSpan.FromMinutes(10),
            PublishedAt = DateTime.UtcNow,
            Status = VideoStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow
        };

        var tag = new Tag
        {
            Name = "C#",
            Slug = "csharp",
            Category = "Languages",
            CreatedAt = DateTime.UtcNow
        };

        context.Videos.Add(video);
        context.Tags.Add(tag);
        await context.SaveChangesAsync();

        var videoTag = new VideoTag
        {
            VideoId = video.Id,
            TagId = tag.Id,
            Source = TagSource.RuleBased,
            Confidence = 0.8f,
            CreatedAt = DateTime.UtcNow
        };

        context.VideoTags.Add(videoTag);
        await context.SaveChangesAsync();

        var tags = await context.VideoTags
            .Where(vt => vt.VideoId == video.Id)
            .Include(vt => vt.Tag)
            .ToListAsync();

        tags.Should().HaveCount(1);
        tags[0].Tag.Name.Should().Be("C#");
    }

    [Fact]
    public async Task CanInsertPlaylistWithVideos()
    {
        await using var context = CreateInMemoryContext();

        var playlist = new Playlist
        {
            YouTubeId = "PLtest123",
            Name = "My Playlist",
            IsInbox = true,
            IsManaged = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow
        };

        var video = new Video
        {
            YouTubeId = "vid001",
            Title = "Video in Playlist",
            Description = "Desc",
            ChannelName = "Ch",
            ChannelId = "UC789",
            ThumbnailUrl = "https://example.com/t.jpg",
            Duration = TimeSpan.FromMinutes(5),
            PublishedAt = DateTime.UtcNow,
            Status = VideoStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow
        };

        context.Playlists.Add(playlist);
        context.Videos.Add(video);
        await context.SaveChangesAsync();

        context.PlaylistVideos.Add(new PlaylistVideo
        {
            PlaylistId = playlist.Id,
            VideoId = video.Id,
            Position = 0,
            AddedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var playlists = await context.Playlists
            .Include(p => p.PlaylistVideos)
            .ThenInclude(pv => pv.Video)
            .ToListAsync();

        playlists.Should().HaveCount(1);
        playlists[0].PlaylistVideos.Should().HaveCount(1);
    }
}
