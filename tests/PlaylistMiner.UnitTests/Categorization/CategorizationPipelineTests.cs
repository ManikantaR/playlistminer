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

        return new CategorizationPipeline(keywordMock, tfidfMock, ollamaMock, db, new PipelineRunTracker(db), NullLogger<CategorizationPipeline>.Instance);
    }

    [Fact]
    public async Task Test_Pipeline_RunsKeywordMatcherFirst()
    {
        // Arrange
        using var db = CreateDb();
        db.Videos.Add(MakeVideo(1));
        await db.SaveChangesAsync();

        var keywordMock = new Mock<IKeywordMatcher>();
        keywordMock.Setup(k => k.MatchAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync([]);

        var pipeline = CreatePipeline(db, keyword: keywordMock.Object);

        // Act
        await pipeline.CategorizeAsync(1);

        // Assert
        keywordMock.Verify(k => k.MatchAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Test_Pipeline_RunsTfIdfSecond()
    {
        // Arrange
        using var db = CreateDb();
        db.Videos.Add(MakeVideo(1));
        await db.SaveChangesAsync();

        var tfidfMock = new Mock<ITfIdfScorer>();
        tfidfMock.Setup(t => t.ScoreAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]);

        var keywordMock = new Mock<IKeywordMatcher>();
        keywordMock.Setup(k => k.MatchAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync([new TagSuggestion(1, "React", 0.8f, TagSource.RuleBased)]);

        var pipeline = CreatePipeline(db, keyword: keywordMock.Object, tfidf: tfidfMock.Object);

        // Act
        await pipeline.CategorizeAsync(1);

        // Assert — TF-IDF runs even when keyword already has results
        tfidfMock.Verify(t => t.ScoreAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Test_Pipeline_RunsOllamaOnlyIfNoSuggestions()
    {
        // Arrange — scenario A: empty keyword+tfidf → Ollama called
        using var dbA = CreateDb();
        var tag = MakeTag(1, "React");
        dbA.Videos.Add(MakeVideo(1));
        dbA.Tags.Add(tag);
        await dbA.SaveChangesAsync();

        var ollamaMockA = new Mock<IOllamaCategorizer>();
        ollamaMockA.Setup(o => o.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        ollamaMockA.Setup(o => o.CategorizeAsync(It.IsAny<VideoContext>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync([]);

        var pipelineA = CreatePipeline(dbA, ollama: ollamaMockA.Object);
        await pipelineA.CategorizeAsync(1);

        ollamaMockA.Verify(o => o.CategorizeAsync(It.IsAny<VideoContext>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);

        // Arrange — scenario B: keyword returns suggestions → Ollama NOT called
        using var dbB = CreateDb();
        dbB.Videos.Add(MakeVideo(1));
        await dbB.SaveChangesAsync();

        var ollamaMockB = new Mock<IOllamaCategorizer>();
        ollamaMockB.Setup(o => o.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var keywordWithResults = new Mock<IKeywordMatcher>();
        keywordWithResults.Setup(k => k.MatchAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync([new TagSuggestion(1, "React", 0.8f, TagSource.RuleBased)]);

        var pipelineB = CreatePipeline(dbB, keyword: keywordWithResults.Object, ollama: ollamaMockB.Object);
        await pipelineB.CategorizeAsync(1);

        ollamaMockB.Verify(o => o.CategorizeAsync(It.IsAny<VideoContext>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
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
