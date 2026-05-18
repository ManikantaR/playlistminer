using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;
using PlaylistMiner.Infrastructure.Services;

namespace PlaylistMiner.UnitTests.Services;

[Trait("Category", "Unit")]
public class VideoServiceTests
{
    private static PlaylistMinerDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<PlaylistMinerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlaylistMinerDbContext(opts);
    }

    private static Video MakeVideo(int id = 1) => new()
    {
        Id = id,
        YouTubeId = $"vid{id:D3}",
        Title = $"Video {id}",
        ChannelName = "Channel",
        ChannelId = "UC1",
        Status = VideoStatus.Active,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        SyncedAt = DateTime.UtcNow
    };

    private static Tag MakeTag(int id = 1) => new()
    {
        Id = id,
        Name = $"Tag{id}",
        Slug = $"tag{id}",
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task Test_AcceptTag_CallsSelfLearning_PromotesToManual()
    {
        // Arrange
        using var db = CreateDb();
        var video = MakeVideo(1);
        var tag = MakeTag(1);
        db.Videos.Add(video);
        db.Tags.Add(tag);
        db.VideoTags.Add(new VideoTag
        {
            VideoId = 1,
            TagId = 1,
            Source = TagSource.Suggested,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var selfLearning = new Mock<ISelfLearningService>();
        var svc = new VideoService(db, selfLearning.Object);

        // Act
        await svc.AcceptTagAsync(1, 1);

        // Assert
        var vt = await db.VideoTags.FirstAsync(x => x.VideoId == 1 && x.TagId == 1);
        vt.Source.Should().Be(TagSource.Manual);
        selfLearning.Verify(s => s.OnTagAcceptedAsync(1, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Test_RejectTag_CallsSelfLearning_RemovesSuggestion()
    {
        // Arrange
        using var db = CreateDb();
        var video = MakeVideo(1);
        var tag = MakeTag(1);
        db.Videos.Add(video);
        db.Tags.Add(tag);
        db.VideoTags.Add(new VideoTag
        {
            VideoId = 1,
            TagId = 1,
            Source = TagSource.Suggested,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var selfLearning = new Mock<ISelfLearningService>();
        var svc = new VideoService(db, selfLearning.Object);

        // Act
        await svc.RejectTagAsync(1, 1);

        // Assert
        var exists = await db.VideoTags.AnyAsync(vt => vt.VideoId == 1 && vt.TagId == 1);
        exists.Should().BeFalse();
        selfLearning.Verify(s => s.OnTagRejectedAsync(1, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Test_AddTag_CreatesManualVideoTag()
    {
        // Arrange
        using var db = CreateDb();
        var video = MakeVideo(1);
        var tag = MakeTag(1);
        db.Videos.Add(video);
        db.Tags.Add(tag);
        await db.SaveChangesAsync();

        var selfLearning = new Mock<ISelfLearningService>();
        var svc = new VideoService(db, selfLearning.Object);

        // Act
        await svc.AddTagAsync(1, 1);

        // Assert
        var vt = await db.VideoTags.FirstOrDefaultAsync(x => x.VideoId == 1 && x.TagId == 1);
        vt.Should().NotBeNull();
        vt!.Source.Should().Be(TagSource.Manual);
    }

    [Fact]
    public async Task Test_RemoveTag_DeletesVideoTag()
    {
        // Arrange
        using var db = CreateDb();
        var video = MakeVideo(1);
        var tag = MakeTag(1);
        db.Videos.Add(video);
        db.Tags.Add(tag);
        db.VideoTags.Add(new VideoTag
        {
            VideoId = 1,
            TagId = 1,
            Source = TagSource.Manual,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var selfLearning = new Mock<ISelfLearningService>();
        var svc = new VideoService(db, selfLearning.Object);

        // Act
        await svc.RemoveTagAsync(1, 1);

        // Assert
        var exists = await db.VideoTags.AnyAsync(vt => vt.VideoId == 1 && vt.TagId == 1);
        exists.Should().BeFalse();
    }
}
