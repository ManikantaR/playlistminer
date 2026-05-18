using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Repositories;

namespace PlaylistMiner.IntegrationTests.Repositories;

[Trait("Category", "Integration")]
public class TagRepositoryTests : RepositoryTestBase
{
    [Fact]
    public async Task Test_GetAll_IncludesVideoCounts()
    {
        // Arrange
        var repo = new TagRepository(DbContext);
        var tag = DbContext.Tags.First();

        var v1 = MakeVideo("cnt00001", "Count Video 1");
        var v2 = MakeVideo("cnt00002", "Count Video 2");
        DbContext.Videos.AddRange(v1, v2);
        await DbContext.SaveChangesAsync();

        DbContext.VideoTags.AddRange([
            new VideoTag { VideoId = v1.Id, TagId = tag.Id, Source = TagSource.Manual, CreatedAt = DateTime.UtcNow },
            new VideoTag { VideoId = v2.Id, TagId = tag.Id, Source = TagSource.Manual, CreatedAt = DateTime.UtcNow }
        ]);
        await DbContext.SaveChangesAsync();

        // Act
        var tags = await repo.GetAllAsync();

        // Assert
        var tagDto = tags.First(t => t.Id == tag.Id);
        tagDto.VideoCount.Should().Be(2);
    }

    [Fact]
    public async Task Test_Create_GeneratesSlug()
    {
        // Arrange
        var repo = new TagRepository(DbContext);

        // Act
        var tag = await repo.CreateAsync(new Tag { Name = "React Hooks", Slug = string.Empty });

        // Assert
        tag.Slug.Should().Be("react-hooks");
        tag.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Test_Create_DuplicateName_Throws()
    {
        // Arrange
        var repo = new TagRepository(DbContext);
        var existingName = DbContext.Tags.First().Name;

        // Act & Assert
        await repo.Invoking(r => r.CreateAsync(new Tag { Name = existingName, Slug = string.Empty }))
            .Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task Test_Delete_RemovesAssociations()
    {
        // Arrange
        var repo = new TagRepository(DbContext);
        var newTag = await repo.CreateAsync(new Tag { Name = "DeleteMe", Slug = string.Empty });
        var video = MakeVideo("del00001", "Video to Test Delete");
        DbContext.Videos.Add(video);
        await DbContext.SaveChangesAsync();

        DbContext.VideoTags.Add(new VideoTag
        {
            VideoId = video.Id,
            TagId = newTag.Id,
            Source = TagSource.Manual,
            CreatedAt = DateTime.UtcNow
        });
        await DbContext.SaveChangesAsync();

        // Act
        await repo.DeleteAsync(newTag.Id);

        // Assert
        DbContext.ChangeTracker.Clear();
        var tagExists = await DbContext.Tags.AnyAsync(t => t.Id == newTag.Id);
        tagExists.Should().BeFalse();

        var assocExists = await DbContext.VideoTags.AnyAsync(vt => vt.TagId == newTag.Id);
        assocExists.Should().BeFalse();
    }
}
