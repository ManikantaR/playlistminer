using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PlaylistMiner.Core.Categorization;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Categorization;
using PlaylistMiner.Infrastructure.Data;
using PlaylistMiner.Infrastructure.Services;

namespace PlaylistMiner.UnitTests.Categorization;

[Trait("Category", "Unit")]
public class CategorizationPipelineTests
{
    private static PlaylistMinerDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<PlaylistMinerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlaylistMinerDbContext(opts);
    }

    private static Video MakeVideo(int id, string title = "React Tutorial", string desc = "Learn React") =>
        new Video
        {
            Id = id,
            YouTubeId = $"yt{id}",
            Title = title,
            Description = desc,
            ChannelName = "Channel",
            ChannelId = "UC1",
            ThumbnailUrl = "https://thumb.jpg",
            Status = VideoStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow
        };

    private static Tag MakeTag(int id, string name) =>
        new Tag { Id = id, Name = name, Slug = name.ToLower(), CreatedAt = DateTime.UtcNow };

    private static CategorizationPipeline CreatePipeline(
        PlaylistMinerDbContext db,
        IKeywordMatcher? keyword = null,
        ITfIdfScorer? tfidf = null,
        IOllamaCategorizer? ollama = null)
    {
        var keywordMock = keyword ?? Mock.Of<IKeywordMatcher>(m =>
            m.MatchAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()) == Task.FromResult(new List<TagSuggestion>()));
        var tfidfMock = tfidf ?? Mock.Of<ITfIdfScorer>(m =>
            m.ScoreAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()) == Task.FromResult(new List<TagSuggestion>()));
        var ollamaMock = ollama ?? Mock.Of<IOllamaCategorizer>(m =>
            m.IsAvailableAsync(It.IsAny<CancellationToken>()) == Task.FromResult(false));

        return new CategorizationPipeline(
            keywordMock,
            tfidfMock,
            ollamaMock,
            db,
            new PipelineRunTracker(db),
            NullLogger<CategorizationPipeline>.Instance);
    }

    [Fact]
    public async Task Test_ClassifyAsync_UsesOllamaFirstWhenReachable()
    {
        // Arrange
        using var db = CreateDb();
        db.Tags.Add(MakeTag(1, "React"));
        db.Videos.Add(MakeVideo(1));
        await db.SaveChangesAsync();

        var keywordMock = new Mock<IKeywordMatcher>();
        keywordMock.Setup(k => k.MatchAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync([new TagSuggestion(1, "React", 0.6f, TagSource.RuleBased)]);

        var tfidfMock = new Mock<ITfIdfScorer>();
        tfidfMock.Setup(t => t.ScoreAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([new TagSuggestion(1, "React", 0.55f, TagSource.TfIdf)]);

        var ollamaMock = new Mock<IOllamaCategorizer>();
        ollamaMock.Setup(o => o.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        ollamaMock.Setup(o => o.CategorizeAsync(It.IsAny<VideoContext>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([new TagSuggestion(0, "React", 0.91f, TagSource.Ollama)]);

        var pipeline = CreatePipeline(db, keyword: keywordMock.Object, tfidf: tfidfMock.Object, ollama: ollamaMock.Object);

        // Act
        var result = await pipeline.ClassifyAsync(1);

        // Assert
        result.Should().ContainSingle();
        result[0].TagName.Should().Be("React");
        result[0].Confidence.Should().BeApproximately(0.91f, 0.001f);
        result[0].Source.Should().Be(TagSource.Ollama);
        ollamaMock.Verify(o => o.CategorizeAsync(It.IsAny<VideoContext>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
        keywordMock.Verify(k => k.MatchAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()), Times.Never);
        tfidfMock.Verify(t => t.ScoreAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Test_ClassifyAsync_FallsBackWhenOllamaUnavailable()
    {
        // Arrange
        using var db = CreateDb();
        db.Videos.Add(MakeVideo(1));
        await db.SaveChangesAsync();

        var tfidfMock = new Mock<ITfIdfScorer>();
        tfidfMock.Setup(t => t.ScoreAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([new TagSuggestion(2, "TypeScript", 0.72f, TagSource.TfIdf)]);

        var keywordMock = new Mock<IKeywordMatcher>();
        keywordMock.Setup(k => k.MatchAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync([new TagSuggestion(1, "React", 0.8f, TagSource.RuleBased)]);

        var ollamaMock = new Mock<IOllamaCategorizer>();
        ollamaMock.Setup(o => o.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var pipeline = CreatePipeline(db, keyword: keywordMock.Object, tfidf: tfidfMock.Object, ollama: ollamaMock.Object);

        // Act
        var result = await pipeline.ClassifyAsync(1);

        // Assert
        result.Should().HaveCount(2);
        result[0].TagName.Should().Be("React");
        result[1].TagName.Should().Be("TypeScript");
        ollamaMock.Verify(o => o.CategorizeAsync(It.IsAny<VideoContext>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        keywordMock.Verify(k => k.MatchAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()), Times.Once);
        tfidfMock.Verify(t => t.ScoreAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Test_ClassifyAsync_FallsBackWhenOllamaReturnsMalformedOrUnknownTags()
    {
        // Arrange
        using var db = CreateDb();
        db.Tags.AddRange(MakeTag(1, "React"), MakeTag(2, "TypeScript"));
        db.Videos.Add(MakeVideo(1));
        await db.SaveChangesAsync();

        var keywordMock = new Mock<IKeywordMatcher>();
        keywordMock.Setup(k => k.MatchAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync([new TagSuggestion(1, "React", 0.64f, TagSource.RuleBased)]);

        var tfidfMock = new Mock<ITfIdfScorer>();
        tfidfMock.Setup(t => t.ScoreAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([new TagSuggestion(2, "TypeScript", 0.61f, TagSource.TfIdf)]);

        var ollamaMock = new Mock<IOllamaCategorizer>();
        ollamaMock.Setup(o => o.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        ollamaMock.Setup(o => o.CategorizeAsync(It.IsAny<VideoContext>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([]);

        var pipeline = CreatePipeline(db, keyword: keywordMock.Object, tfidf: tfidfMock.Object, ollama: ollamaMock.Object);

        // Act
        var result = await pipeline.ClassifyAsync(1);

        // Assert
        result.Should().HaveCount(2);
        result.Select(x => x.TagName).Should().Contain(["React", "TypeScript"]);
        keywordMock.Verify(k => k.MatchAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()), Times.Once);
        tfidfMock.Verify(t => t.ScoreAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Test_Pipeline_MergesSuggestions_KeepsHighestConfidence()
    {
        // Arrange
        using var db = CreateDb();
        db.Videos.Add(MakeVideo(1));
        await db.SaveChangesAsync();

        var keywordMock = new Mock<IKeywordMatcher>();
        keywordMock.Setup(k => k.MatchAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync([new TagSuggestion(1, "React", 0.7f, TagSource.RuleBased)]);

        var tfidfMock = new Mock<ITfIdfScorer>();
        tfidfMock.Setup(t => t.ScoreAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([new TagSuggestion(1, "React", 0.9f, TagSource.TfIdf)]);

        var pipeline = CreatePipeline(db, keyword: keywordMock.Object, tfidf: tfidfMock.Object);

        // Act
        var result = await pipeline.CategorizeAsync(1);

        // Assert
        result.Should().ContainSingle(s => s.TagId == 1);
        result[0].Confidence.Should().BeApproximately(0.9f, 0.001f);
    }

    [Fact]
    public async Task Test_Pipeline_DeduplicatesTags()
    {
        // Arrange
        using var db = CreateDb();
        db.Videos.Add(MakeVideo(1));
        await db.SaveChangesAsync();

        var keywordMock = new Mock<IKeywordMatcher>();
        keywordMock.Setup(k => k.MatchAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync([new TagSuggestion(1, "React", 0.7f, TagSource.RuleBased)]);

        var tfidfMock = new Mock<ITfIdfScorer>();
        tfidfMock.Setup(t => t.ScoreAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([new TagSuggestion(1, "React", 0.9f, TagSource.TfIdf)]);

        var pipeline = CreatePipeline(db, keyword: keywordMock.Object, tfidf: tfidfMock.Object);

        // Act
        await pipeline.CategorizeAsync(1);

        // Assert — only one VideoTag saved (not two)
        var videoTags = await db.VideoTags.Where(vt => vt.VideoId == 1).ToListAsync();
        videoTags.Should().HaveCount(1);
    }

    [Fact]
    public async Task Test_Pipeline_SavesSuggestionsToDatabase()
    {
        // Arrange
        using var db = CreateDb();
        db.Videos.Add(MakeVideo(1));
        await db.SaveChangesAsync();

        var keywordMock = new Mock<IKeywordMatcher>();
        keywordMock.Setup(k => k.MatchAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync([new TagSuggestion(1, "React", 0.8f, TagSource.RuleBased)]);

        var pipeline = CreatePipeline(db, keyword: keywordMock.Object);

        // Act
        await pipeline.CategorizeAsync(1);

        // Assert
        var saved = await db.VideoTags.FirstOrDefaultAsync(vt => vt.VideoId == 1 && vt.TagId == 1);
        saved.Should().NotBeNull();
        saved!.Confidence.Should().BeApproximately(0.8f, 0.001f);
    }

    [Fact]
    public async Task Test_Pipeline_SkipsAlreadyTaggedVideos()
    {
        // Arrange
        using var db = CreateDb();
        db.Videos.Add(MakeVideo(1));
        db.Tags.Add(MakeTag(1, "React"));
        db.VideoTags.Add(new VideoTag
        {
            VideoId = 1,
            TagId = 1,
            Source = TagSource.Manual,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var keywordMock = new Mock<IKeywordMatcher>();

        var pipeline = CreatePipeline(db, keyword: keywordMock.Object);

        // Act
        var result = await pipeline.CategorizeAsync(1);

        // Assert
        result.Should().BeEmpty();
        keywordMock.Verify(k => k.MatchAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
