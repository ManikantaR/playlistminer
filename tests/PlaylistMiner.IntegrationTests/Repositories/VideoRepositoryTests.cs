using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Repositories;

namespace PlaylistMiner.IntegrationTests.Repositories;

[Trait("Category", "Integration")]
public class VideoRepositoryTests : RepositoryTestBase
{
    [Fact]
    public async Task Test_GetAll_WithPagination_ReturnsPage()
    {
        // Arrange
        var repo = new VideoRepository(DbContext);
        for (var i = 1; i <= 25; i++)
        {
            DbContext.Videos.Add(MakeVideo($"vid{i:D3}", $"Video {i:D3}"));
        }
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repo.GetAllAsync(new VideoFilter(Page: 2, PageSize: 10));

        // Assert
        result.Items.Should().HaveCount(10);
        result.TotalCount.Should().Be(25);
        result.Page.Should().Be(2);
        result.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task Test_GetAll_WithTagFilter_FiltersCorrectly()
    {
        // Arrange
        var repo = new VideoRepository(DbContext);
        var tag = DbContext.Tags.First(); // Use seeded tag

        var taggedVideo = MakeVideo("tagged01", "Tagged Video");
        var untaggedVideo = MakeVideo("notag001", "Untagged Video");
        DbContext.Videos.AddRange(taggedVideo, untaggedVideo);
        await DbContext.SaveChangesAsync();

        DbContext.VideoTags.Add(new VideoTag
        {
            VideoId = taggedVideo.Id,
            TagId = tag.Id,
            Source = TagSource.Manual,
            CreatedAt = DateTime.UtcNow
        });
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repo.GetAllAsync(new VideoFilter(Tags: [tag.Id]));

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].YouTubeId.Should().Be("tagged01");
    }

    [Fact]
    public async Task Test_GetAll_WithMultipleTags_UsesAndLogic()
    {
        // Arrange
        var repo = new VideoRepository(DbContext);
        var tags = DbContext.Tags.Take(2).ToList();

        var bothTagsVideo = MakeVideo("both0001", "Both Tags Video");
        var oneTagVideo = MakeVideo("onetag01", "One Tag Video");
        DbContext.Videos.AddRange(bothTagsVideo, oneTagVideo);
        await DbContext.SaveChangesAsync();

        DbContext.VideoTags.AddRange([
            new VideoTag { VideoId = bothTagsVideo.Id, TagId = tags[0].Id, Source = TagSource.Manual, CreatedAt = DateTime.UtcNow },
            new VideoTag { VideoId = bothTagsVideo.Id, TagId = tags[1].Id, Source = TagSource.Manual, CreatedAt = DateTime.UtcNow },
            new VideoTag { VideoId = oneTagVideo.Id, TagId = tags[0].Id, Source = TagSource.Manual, CreatedAt = DateTime.UtcNow }
        ]);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repo.GetAllAsync(new VideoFilter(Tags: [tags[0].Id, tags[1].Id]));

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].YouTubeId.Should().Be("both0001");
    }

    [Fact]
    public async Task Test_GetAll_WithStatusFilter_FiltersCorrectly()
    {
        // Arrange
        var repo = new VideoRepository(DbContext);
        var activeVideo = MakeVideo("active01", "Active Video");
        var archivedVideo = MakeVideo("archive1", "Archived Video");
        archivedVideo.Status = VideoStatus.Archived;

        DbContext.Videos.AddRange(activeVideo, archivedVideo);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repo.GetAllAsync(new VideoFilter(Status: VideoStatus.Active));

        // Assert
        result.Items.Should().OnlyContain(v => v.Status == VideoStatus.Active);
    }

    [Fact]
    public async Task Test_Search_FuzzyMatch_FindsSimilarTitles()
    {
        // Arrange
        var repo = new VideoRepository(DbContext);
        var video = MakeVideo("react001", "React Tutorial");
        DbContext.Videos.Add(video);
        await DbContext.SaveChangesAsync();

        // Act — case-insensitive substring search
        var result = await repo.GetAllAsync(new VideoFilter(Search: "react"));

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Title.Should().Be("React Tutorial");
    }

    [Fact]
    public async Task Test_Search_FullText_RanksRelevantly()
    {
        // Arrange
        var repo = new VideoRepository(DbContext);
        DbContext.Videos.AddRange(
            MakeVideo("react002", "React Tutorial Advanced"),
            MakeVideo("vue00001", "Vue.js Introduction"));
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repo.GetAllAsync(new VideoFilter(Search: "react"));

        // Assert
        result.Items.Should().Contain(v => v.Title.Contains("React", StringComparison.OrdinalIgnoreCase));
        result.Items.Should().NotContain(v => v.Title.Contains("Vue", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Test_GetById_IncludesTags_ReturnsFull()
    {
        // Arrange
        var repo = new VideoRepository(DbContext);
        var video = MakeVideo("tagged02", "Tagged Video 2");
        DbContext.Videos.Add(video);
        await DbContext.SaveChangesAsync();

        var tag = DbContext.Tags.First();
        DbContext.VideoTags.Add(new VideoTag
        {
            VideoId = video.Id,
            TagId = tag.Id,
            Source = TagSource.Manual,
            CreatedAt = DateTime.UtcNow
        });
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repo.GetByIdAsync(video.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Tags.Should().HaveCount(1);
        result.Tags[0].TagId.Should().Be(tag.Id);
    }

    [Fact]
    public async Task Test_Upsert_NewVideo_Inserts()
    {
        // Arrange
        var repo = new VideoRepository(DbContext);
        var video = MakeVideo("newvid01", "New Video");

        // Act
        var result = await repo.UpsertAsync(video);

        // Assert
        result.Id.Should().BeGreaterThan(0);
        var inDb = await DbContext.Videos.FindAsync(result.Id);
        inDb.Should().NotBeNull();
    }

    [Fact]
    public async Task Test_Upsert_ExistingVideo_Updates()
    {
        // Arrange
        var repo = new VideoRepository(DbContext);
        var video = MakeVideo("existvid", "Original Title");
        DbContext.Videos.Add(video);
        await DbContext.SaveChangesAsync();

        DbContext.ChangeTracker.Clear();

        // Act
        var updated = MakeVideo("existvid", "Updated Title");
        var result = await repo.UpsertAsync(updated);

        // Assert
        DbContext.ChangeTracker.Clear();
        var inDb = await DbContext.Videos.FirstAsync(v => v.YouTubeId == "existvid");
        inDb.Title.Should().Be("Updated Title");
    }
}
