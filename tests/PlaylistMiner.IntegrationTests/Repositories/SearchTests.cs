using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Repositories;

namespace PlaylistMiner.IntegrationTests.Repositories;

[Trait("Category", "Integration")]
public class SearchTests : RepositoryTestBase
{
    private async Task SeedVideos()
    {
        var videos = new[]
        {
            MakeVideo("vid_react", "React Tutorial for Beginners"),
            MakeVideo("vid_csharp", "C# ASP.NET Core Web API Tutorial"),
            MakeVideo("vid_docker", "Docker Containerization Deep Dive"),
            MakeVideo("vid_python", "Python Machine Learning Basics"),
            MakeVideo("vid_ts", "TypeScript Advanced Patterns"),
        };

        videos[0].Description = "Learn React hooks, components, and state management with practical examples";
        videos[1].Description = "Build REST APIs with ASP.NET Core and Entity Framework in C#";
        videos[2].Description = "Container orchestration with Docker Compose and Kubernetes";
        videos[3].Description = "Introduction to scikit-learn, pandas, and neural networks in Python";
        videos[4].Description = "Generics, mapped types, conditional types, and template literals in TypeScript";

        DbContext.Videos.AddRange(videos);
        await DbContext.SaveChangesAsync();

        // Set lower similarity threshold for tests
        await DbContext.Database.ExecuteSqlRawAsync("SET pg_trgm.similarity_threshold = 0.1;");
    }

    [Fact]
    public async Task Test_FuzzySearch_FindsPartialMatch()
    {
        await SeedVideos();
        var repo = new VideoRepository(DbContext);

        var result = await repo.GetAllAsync(new VideoFilter(Search: "React"));

        result.Items.Should().Contain(v => v.Title.Contains("React"));
    }

    [Fact]
    public async Task Test_FuzzySearch_RanksExactMatchHigher()
    {
        await SeedVideos();
        var repo = new VideoRepository(DbContext);

        var result = await repo.GetAllAsync(new VideoFilter(Search: "React Tutorial"));

        result.Items.Should().NotBeEmpty();
        result.Items.First().Title.Should().Contain("React");
    }

    [Fact]
    public async Task Test_FullTextSearch_FindsDescriptionMatch()
    {
        await SeedVideos();
        var repo = new VideoRepository(DbContext);

        var result = await repo.GetAllAsync(new VideoFilter(Search: "scikit-learn pandas"));

        result.Items.Should().Contain(v => v.YouTubeId == "vid_python");
    }

    [Fact]
    public async Task Test_Search_WithTagFilter_CombinesCorrectly()
    {
        await SeedVideos();

        var tag = new Tag
        {
            Name = "SearchTestReact",
            Slug = "search-test-react",
            Category = "Frontend",
            CreatedAt = DateTime.UtcNow
        };
        DbContext.Tags.Add(tag);
        await DbContext.SaveChangesAsync();

        var video = await DbContext.Videos.FirstAsync(v => v.YouTubeId == "vid_react");
        DbContext.VideoTags.Add(new VideoTag
        {
            VideoId = video.Id,
            TagId = tag.Id,
            Source = TagSource.Manual,
            CreatedAt = DateTime.UtcNow
        });
        await DbContext.SaveChangesAsync();

        var repo = new VideoRepository(DbContext);
        var result = await repo.GetAllAsync(new VideoFilter(Search: "Tutorial", Tags: [tag.Id]));

        result.Items.Should().HaveCount(1);
        result.Items.First().YouTubeId.Should().Be("vid_react");
    }

    [Fact]
    public async Task Test_Search_EmptyQuery_ReturnsAll()
    {
        await SeedVideos();
        var repo = new VideoRepository(DbContext);

        var result = await repo.GetAllAsync(new VideoFilter());

        result.Items.Should().HaveCount(5);
    }

    [Fact]
    public async Task Test_Search_NoResults_ReturnsEmpty()
    {
        await SeedVideos();
        var repo = new VideoRepository(DbContext);

        var result = await repo.GetAllAsync(new VideoFilter(Search: "zzzzxyznonexistent"));

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Test_Search_SpecialCharacters_HandledSafely()
    {
        await SeedVideos();
        var repo = new VideoRepository(DbContext);

        var resultCsharp = await repo.GetAllAsync(new VideoFilter(Search: "C#"));
        resultCsharp.Items.Should().Contain(v => v.Title.Contains("C#"));

        var resultAspnet = await repo.GetAllAsync(new VideoFilter(Search: "ASP.NET"));
        resultAspnet.Items.Should().Contain(v => v.Title.Contains("ASP.NET"));
    }

    [Fact]
    public async Task Test_Search_SqlInjection_HandledSafely()
    {
        await SeedVideos();
        var repo = new VideoRepository(DbContext);

        var act = async () => await repo.GetAllAsync(
            new VideoFilter(Search: "'; DROP TABLE videos; --"));

        await act.Should().NotThrowAsync();
    }
}
