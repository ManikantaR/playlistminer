using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
        IOllamaCategorizer? ollama = null,
        CategorizationOptions? options = null)
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
            Options.Create(options ?? new CategorizationOptions()),
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

    [Fact]
    public async Task Test_ReclassifyGenerated_RemovesGeneratedTags_AndRecategorizesActiveVideosWithoutManualTags()
    {
        // Arrange
        using var db = CreateDb();
        var archivedVideo = MakeVideo(3, "Archived React");
        archivedVideo.Status = VideoStatus.Archived;

        db.Tags.AddRange(MakeTag(1, "React"), MakeTag(2, "Python"));
        db.Videos.AddRange(
            MakeVideo(1, "Old React"),
            MakeVideo(2, "Manual Python"),
            archivedVideo);
        db.VideoTags.AddRange(
            new VideoTag
            {
                VideoId = 1,
                TagId = 2,
                Source = TagSource.RuleBased,
                Confidence = 1.0f,
                CreatedAt = DateTime.UtcNow
            },
            new VideoTag
            {
                VideoId = 2,
                TagId = 2,
                Source = TagSource.Manual,
                CreatedAt = DateTime.UtcNow
            },
            new VideoTag
            {
                VideoId = 2,
                TagId = 1,
                Source = TagSource.RuleBased,
                Confidence = 1.0f,
                CreatedAt = DateTime.UtcNow
            },
            new VideoTag
            {
                VideoId = 3,
                TagId = 1,
                Source = TagSource.RuleBased,
                Confidence = 1.0f,
                CreatedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var keywordMock = new Mock<IKeywordMatcher>();
        keywordMock.Setup(k => k.MatchAsync(
                It.Is<VideoContext>(v => v.Title == "Old React"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TagSuggestion(1, "React", 0.8f, TagSource.RuleBased)]);

        var pipeline = CreatePipeline(db, keyword: keywordMock.Object);

        // Act
        await pipeline.ReclassifyGeneratedAsync();

        // Assert
        var activeTags = await db.VideoTags
            .Where(vt => vt.VideoId == 1 || vt.VideoId == 2)
            .OrderBy(vt => vt.VideoId)
            .ThenBy(vt => vt.Source)
            .ThenBy(vt => vt.TagId)
            .ToListAsync();

        activeTags.Should().BeEquivalentTo([
            new { VideoId = 1, TagId = 1, Source = TagSource.RuleBased },
            new { VideoId = 2, TagId = 2, Source = TagSource.Manual }
        ], opts => opts.ExcludingMissingMembers());

        var archivedTag = await db.VideoTags.SingleAsync(vt => vt.VideoId == 3);
        archivedTag.TagId.Should().Be(1);
        archivedTag.Source.Should().Be(TagSource.RuleBased);

        keywordMock.Verify(k => k.MatchAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Test_ReclassifyGenerated_RecordsPipelineRunMetrics()
    {
        // Arrange
        using var db = CreateDb();
        db.Tags.Add(MakeTag(1, "React"));
        db.Videos.AddRange(MakeVideo(1), MakeVideo(2, "No Match"));
        await db.SaveChangesAsync();

        var keywordMock = new Mock<IKeywordMatcher>();
        keywordMock.Setup(k => k.MatchAsync(
                It.Is<VideoContext>(v => v.Title == "React Tutorial"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TagSuggestion(1, "React", 0.8f, TagSource.RuleBased)]);
        keywordMock.Setup(k => k.MatchAsync(
                It.Is<VideoContext>(v => v.Title == "No Match"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var pipeline = CreatePipeline(db, keyword: keywordMock.Object);

        // Act
        await pipeline.ReclassifyGeneratedAsync();

        // Assert
        var run = await db.PipelineRuns.SingleAsync(r => r.PipelineType == "reclassification");
        run.Status.Should().Be("completed");
        run.VideosPendingTagging.Should().Be(2);
        run.VideosProcessed.Should().Be(2);
        run.VideosTagged.Should().Be(1);
        run.VideosSkipped.Should().Be(1);
        run.RuleBasedHits.Should().Be(1);
    }

    [Fact]
    public async Task Test_CategorizeNewVideos_WhenBacklogExceedsBatchLimit_ProcessesConfiguredBatchOnly()
    {
        // Arrange
        using var db = CreateDb();
        db.Tags.Add(MakeTag(1, "React"));
        db.Videos.AddRange(
            MakeVideo(1),
            MakeVideo(2),
            MakeVideo(3));
        await db.SaveChangesAsync();

        var keywordMock = new Mock<IKeywordMatcher>();
        keywordMock.Setup(k => k.MatchAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TagSuggestion(1, "React", 0.8f, TagSource.RuleBased)]);

        var pipeline = CreatePipeline(
            db,
            keyword: keywordMock.Object,
            options: new CategorizationOptions { MaxVideosPerRun = 2 });

        // Act
        await pipeline.CategorizeNewVideosAsync();

        // Assert
        var run = await db.PipelineRuns.SingleAsync(r => r.PipelineType == "categorization");
        run.Status.Should().Be("completed");
        run.VideosPendingTagging.Should().Be(3);
        run.VideosProcessed.Should().Be(2);
        run.VideosTagged.Should().Be(2);
        run.CurrentMessage.Should().Be("Run completed successfully.");

        var savedTags = await db.VideoTags.OrderBy(vt => vt.VideoId).ToListAsync();
        savedTags.Select(vt => vt.VideoId).Should().Equal(1, 2);
        keywordMock.Verify(k => k.MatchAsync(It.IsAny<VideoContext>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
