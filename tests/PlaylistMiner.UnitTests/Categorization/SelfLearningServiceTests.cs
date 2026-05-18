using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Categorization;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.UnitTests.Categorization;

[Trait("Category", "Unit")]
public class SelfLearningServiceTests
{
    private static PlaylistMinerDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<PlaylistMinerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlaylistMinerDbContext(opts);
    }

    private static Video MakeVideo(int id, string title, string description = "") =>
        new Video
        {
            Id = id,
            YouTubeId = $"yt{id}",
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

    private static SelfLearningService CreateService(PlaylistMinerDbContext db, ITfIdfScorer? tfIdfScorer = null)
    {
        var scorer = tfIdfScorer ?? Mock.Of<ITfIdfScorer>();
        return new SelfLearningService(db, scorer, NullLogger<SelfLearningService>.Instance);
    }

    [Fact]
    public async Task Test_OnTagAccepted_ExtractsKeywordsFromTitle()
    {
        // Arrange
        using var db = CreateDb();
        db.Videos.Add(MakeVideo(1, "Learn React Hooks Tutorial"));
        db.Tags.Add(new Tag { Id = 1, Name = "React", Slug = "react", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act
        await service.OnTagAcceptedAsync(1, 1);

        // Assert — extracted significant words (>=3 chars, not stop words)
        var rules = await db.TagRules.Where(r => r.TagId == 1).ToListAsync();
        rules.Should().NotBeEmpty();
        var keywords = rules.Select(r => r.Keyword).ToList();
        // "Learn", "React", "Hooks", "Tutorial" should all be extracted (case insensitive)
        keywords.Should().Contain(k => k.Equals("learn", StringComparison.OrdinalIgnoreCase));
        keywords.Should().Contain(k => k.Equals("react", StringComparison.OrdinalIgnoreCase));
        keywords.Should().Contain(k => k.Equals("hooks", StringComparison.OrdinalIgnoreCase));
        keywords.Should().Contain(k => k.Equals("tutorial", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Test_OnTagAccepted_CreatesNewLearnedRules()
    {
        // Arrange
        using var db = CreateDb();
        db.Videos.Add(MakeVideo(1, "React programming guide"));
        db.Tags.Add(new Tag { Id = 1, Name = "React", Slug = "react", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act
        await service.OnTagAcceptedAsync(1, 1);

        // Assert
        var reactRule = await db.TagRules.FirstOrDefaultAsync(r => r.TagId == 1 && r.Keyword == "react");
        reactRule.Should().NotBeNull();
        reactRule!.IsLearned.Should().BeTrue();
        reactRule.Weight.Should().BeApproximately(0.3f, 0.001f);
        reactRule.Field.Should().Be(TagRuleField.Both);
    }

    [Fact]
    public async Task Test_OnTagAccepted_IncrementsExistingRuleWeight()
    {
        // Arrange
        using var db = CreateDb();
        db.Videos.Add(MakeVideo(1, "React advanced tutorial"));
        db.Tags.Add(new Tag { Id = 1, Name = "React", Slug = "react", CreatedAt = DateTime.UtcNow });
        db.TagRules.Add(new TagRule
        {
            Id = 1,
            TagId = 1,
            Keyword = "react",
            Field = TagRuleField.Both,
            Weight = 0.5f,
            IsLearned = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act
        await service.OnTagAcceptedAsync(1, 1);

        // Assert
        var rule = await db.TagRules.FirstAsync(r => r.TagId == 1 && r.Keyword == "react");
        rule.Weight.Should().BeApproximately(0.6f, 0.001f);
    }

    [Fact]
    public async Task Test_OnTagAccepted_CapsWeightAt1()
    {
        // Arrange
        using var db = CreateDb();
        db.Videos.Add(MakeVideo(1, "React advanced patterns"));
        db.Tags.Add(new Tag { Id = 1, Name = "React", Slug = "react", CreatedAt = DateTime.UtcNow });
        db.TagRules.Add(new TagRule
        {
            Id = 1,
            TagId = 1,
            Keyword = "react",
            Field = TagRuleField.Both,
            Weight = 0.95f,
            IsLearned = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act
        await service.OnTagAcceptedAsync(1, 1);

        // Assert
        var rule = await db.TagRules.FirstAsync(r => r.TagId == 1 && r.Keyword == "react");
        rule.Weight.Should().BeApproximately(1.0f, 0.001f);
    }

    [Fact]
    public async Task Test_OnTagRejected_DecrementsRuleWeights()
    {
        // Arrange
        using var db = CreateDb();
        db.Videos.Add(MakeVideo(1, "React component lifecycle"));
        db.Tags.Add(new Tag { Id = 1, Name = "React", Slug = "react", CreatedAt = DateTime.UtcNow });
        db.TagRules.Add(new TagRule
        {
            Id = 1,
            TagId = 1,
            Keyword = "react",
            Field = TagRuleField.Both,
            Weight = 0.5f,
            IsLearned = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act
        await service.OnTagRejectedAsync(1, 1);

        // Assert
        var rule = await db.TagRules.FirstOrDefaultAsync(r => r.TagId == 1 && r.Keyword == "react");
        rule.Should().NotBeNull();
        rule!.Weight.Should().BeApproximately(0.4f, 0.001f);
    }

    [Fact]
    public async Task Test_OnTagRejected_RemovesRulesAtZeroWeight()
    {
        // Arrange
        using var db = CreateDb();
        db.Videos.Add(MakeVideo(1, "React lifecycle methods"));
        db.Tags.Add(new Tag { Id = 1, Name = "React", Slug = "react", CreatedAt = DateTime.UtcNow });
        db.TagRules.Add(new TagRule
        {
            Id = 1,
            TagId = 1,
            Keyword = "react",
            Field = TagRuleField.Both,
            Weight = 0.1f,
            IsLearned = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Act
        await service.OnTagRejectedAsync(1, 1);

        // Assert — rule deleted
        var rule = await db.TagRules.FirstOrDefaultAsync(r => r.TagId == 1 && r.Keyword == "react");
        rule.Should().BeNull();
    }

    [Fact]
    public async Task Test_OnTagAccepted_InvalidatesTfIdfCorpus()
    {
        // Arrange
        using var db = CreateDb();
        db.Videos.Add(MakeVideo(1, "React hooks deep dive"));
        db.Tags.Add(new Tag { Id = 1, Name = "React", Slug = "react", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var tfIdfMock = new Mock<ITfIdfScorer>();
        tfIdfMock.Setup(t => t.BuildCorpusAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var service = CreateService(db, tfIdfMock.Object);

        // Act
        await service.OnTagAcceptedAsync(1, 1);

        // Assert
        tfIdfMock.Verify(t => t.BuildCorpusAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
