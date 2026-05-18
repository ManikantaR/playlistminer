using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PlaylistMiner.Core.Categorization;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Categorization;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.UnitTests.Categorization;

[Trait("Category", "Unit")]
public class TfIdfScorerTests
{
    private static PlaylistMinerDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<PlaylistMinerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlaylistMinerDbContext(opts);
    }

    private static IMemoryCache CreateCache() => new MemoryCache(new MemoryCacheOptions());

    private static IOptions<CategorizationOptions> DefaultOptions(float tfidfThreshold = 0.3f)
        => Options.Create(new CategorizationOptions { TfIdfThreshold = tfidfThreshold });

    private static (Video video, Tag tag, VideoTag videoTag) MakeManualTaggedVideo(
        int videoId, int tagId, string tagName, string title, string description)
    {
        var video = new Video
        {
            Id = videoId,
            YouTubeId = $"yt{videoId}",
            Title = title,
            Description = description,
            ChannelName = "Channel",
            ChannelId = "UC1",
            ThumbnailUrl = "https://thumb.jpg",
            Status = VideoStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow
        };
        var tag = new Tag
        {
            Id = tagId,
            Name = tagName,
            Slug = tagName.ToLowerInvariant(),
            CreatedAt = DateTime.UtcNow
        };
        var videoTag = new VideoTag
        {
            VideoId = videoId,
            TagId = tagId,
            Source = TagSource.Manual,
            Confidence = 1.0f,
            CreatedAt = DateTime.UtcNow
        };
        return (video, tag, videoTag);
    }

    [Fact]
    public async Task Test_Score_EmptyCorpus_ReturnsEmpty()
    {
        // Arrange - no BuildCorpus called
        using var db = CreateDb();
        var cache = CreateCache();
        var scorer = new TfIdfScorer(db, DefaultOptions(), cache);

        var video = new VideoContext("Learn React Hooks", "Building React apps");

        // Act
        var result = await scorer.ScoreAsync(video);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Test_Score_SingleDocument_ComputesSimilarity()
    {
        // Arrange
        using var db = CreateDb();
        var (video, tag, vt) = MakeManualTaggedVideo(1, 1, "React", "React Hooks Tutorial", "Learning React hooks and state");
        db.Videos.Add(video);
        db.Tags.Add(tag);
        db.VideoTags.Add(vt);
        await db.SaveChangesAsync();

        var cache = CreateCache();
        var scorer = new TfIdfScorer(db, DefaultOptions(tfidfThreshold: 0.0f), cache);
        await scorer.BuildCorpusAsync();

        // Act
        var result = await scorer.ScoreAsync(new VideoContext("React hooks tutorial", "Building React applications"));

        // Assert
        result.Should().ContainSingle(s => s.TagId == 1 && s.Confidence > 0);
    }

    [Fact]
    public async Task Test_Score_MultipleDocuments_RanksCorrectly()
    {
        // Arrange
        using var db = CreateDb();
        var (v1, t1, vt1) = MakeManualTaggedVideo(1, 1, "React", "React Component Tutorial", "Learn React components and props");
        var (v2, t2, vt2) = MakeManualTaggedVideo(2, 2, "Python", "Python Data Science", "Machine learning with Python pandas numpy");
        db.Videos.AddRange(v1, v2);
        db.Tags.AddRange(t1, t2);
        db.VideoTags.AddRange(vt1, vt2);
        await db.SaveChangesAsync();

        var cache = CreateCache();
        var scorer = new TfIdfScorer(db, DefaultOptions(tfidfThreshold: 0.0f), cache);
        await scorer.BuildCorpusAsync();

        // Act
        var result = await scorer.ScoreAsync(new VideoContext("React components overview", "Building React user interfaces"));

        // Assert
        result.Should().Contain(s => s.TagId == 1);
        var reactScore = result.First(s => s.TagId == 1).Confidence;
        var pythonScore = result.FirstOrDefault(s => s.TagId == 2)?.Confidence ?? 0f;
        reactScore.Should().BeGreaterThan(pythonScore);
    }

    [Fact]
    public async Task Test_Score_NewVideoSimilarToTag_ReturnsHighConfidence()
    {
        // Arrange
        using var db = CreateDb();
        var (v1, t1, vt1) = MakeManualTaggedVideo(1, 1, "React", "React hooks useState useEffect", "Building React functional components hooks");
        db.Videos.Add(v1);
        db.Tags.Add(t1);
        db.VideoTags.Add(vt1);
        await db.SaveChangesAsync();

        var cache = CreateCache();
        var scorer = new TfIdfScorer(db, DefaultOptions(tfidfThreshold: 0.5f), cache);
        await scorer.BuildCorpusAsync();

        // Act
        var result = await scorer.ScoreAsync(new VideoContext("React hooks useState", "Learning React hooks functional components"));

        // Assert
        result.Should().Contain(s => s.TagId == 1 && s.Confidence > 0.5f);
    }

    [Fact]
    public async Task Test_Score_UnrelatedVideo_ReturnsLowConfidence()
    {
        // Arrange
        using var db = CreateDb();
        var (v1, t1, vt1) = MakeManualTaggedVideo(1, 1, "React", "React hooks useState useEffect", "Building React functional components");
        db.Videos.Add(v1);
        db.Tags.Add(t1);
        db.VideoTags.Add(vt1);
        await db.SaveChangesAsync();

        var cache = CreateCache();
        var scorer = new TfIdfScorer(db, DefaultOptions(tfidfThreshold: 0.5f), cache);
        await scorer.BuildCorpusAsync();

        // Act
        var result = await scorer.ScoreAsync(new VideoContext("Cooking pasta carbonara recipe", "Italian cuisine traditional recipes"));

        // Assert
        result.Should().NotContain(s => s.TagId == 1 && s.Confidence >= 0.5f);
    }

    [Fact]
    public async Task Test_BuildCorpus_GroupsDocumentsByTag()
    {
        // Arrange
        using var db = CreateDb();
        // 2 videos with tag1, 1 video with tag2
        var (v1, t1, vt1) = MakeManualTaggedVideo(1, 1, "React", "React Components tutorial", "Learn React components");
        var (v2, _, _) = MakeManualTaggedVideo(2, 1, "React", "React Hooks advanced", "Advanced React hooks");
        var (v3, t2, vt3) = MakeManualTaggedVideo(3, 2, "Python", "Python basics", "Python programming language");
        // Second video tagged with same tag (tagId=1)
        var vt2 = new VideoTag { VideoId = 2, TagId = 1, Source = TagSource.Manual, Confidence = 1.0f, CreatedAt = DateTime.UtcNow };
        db.Videos.AddRange(v1, v2, v3);
        db.Tags.AddRange(t1, t2);
        db.VideoTags.AddRange(vt1, vt2, vt3);
        await db.SaveChangesAsync();

        var cache = CreateCache();
        var scorer = new TfIdfScorer(db, DefaultOptions(tfidfThreshold: 0.0f), cache);

        // Act
        await scorer.BuildCorpusAsync();
        var reactResult = await scorer.ScoreAsync(new VideoContext("React Components hooks", "Building React interfaces"));
        var pythonResult = await scorer.ScoreAsync(new VideoContext("Python programming", "Python language basics"));

        // Assert - both tags should be represented in corpus (can match)
        reactResult.Should().Contain(s => s.TagId == 1);
        pythonResult.Should().Contain(s => s.TagId == 2);
    }

    [Fact]
    public async Task Test_BuildCorpus_UsesManuallyTaggedVideosOnly()
    {
        // Arrange
        using var db = CreateDb();
        // Manual tag for React
        var (v1, t1, vtManual) = MakeManualTaggedVideo(1, 1, "React", "React hooks tutorial", "React functional components");
        // RuleBased tag for Python - should NOT be used in corpus
        var v2 = new Video
        {
            Id = 2,
            YouTubeId = "yt2",
            Title = "Python machine learning",
            Description = "Python scikit-learn tensorflow keras",
            ChannelName = "Channel",
            ChannelId = "UC1",
            ThumbnailUrl = "https://thumb.jpg",
            Status = VideoStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow
        };
        var t2 = new Tag { Id = 2, Name = "Python", Slug = "python", CreatedAt = DateTime.UtcNow };
        var vtRuleBased = new VideoTag
        {
            VideoId = 2,
            TagId = 2,
            Source = TagSource.RuleBased,
            Confidence = 0.8f,
            CreatedAt = DateTime.UtcNow
        };
        db.Videos.AddRange(v1, v2);
        db.Tags.AddRange(t1, t2);
        db.VideoTags.AddRange(vtManual, vtRuleBased);
        await db.SaveChangesAsync();

        var cache = CreateCache();
        var scorer = new TfIdfScorer(db, DefaultOptions(tfidfThreshold: 0.3f), cache);
        await scorer.BuildCorpusAsync();

        // Act - score a Python-like video
        var result = await scorer.ScoreAsync(new VideoContext("Python machine learning scikit", "Python tensorflow keras deep learning"));

        // Assert - Python tag (id=2) should NOT appear (RuleBased video wasn't included)
        result.Should().NotContain(s => s.TagId == 2);
        // React tag should also not appear (unrelated content)
        result.Should().NotContain(s => s.TagId == 1 && s.Confidence >= 0.3f);
    }
}
