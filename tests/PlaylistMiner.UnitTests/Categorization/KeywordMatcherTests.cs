using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using PlaylistMiner.Core.Categorization;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Categorization;

namespace PlaylistMiner.UnitTests.Categorization;

[Trait("Category", "Unit")]
public class KeywordMatcherTests
{
    private static IOptions<CategorizationOptions> DefaultOptions(float threshold = 0.7f)
        => Options.Create(new CategorizationOptions { KeywordThreshold = threshold });

    private static Tag MakeTag(int id, string name) => new Tag
    {
        Id = id,
        Name = name,
        Slug = name.ToLowerInvariant(),
        CreatedAt = DateTime.UtcNow
    };

    private static TagRule MakeRule(int tagId, string keyword, TagRuleField field, float weight, Tag tag)
        => new TagRule
        {
            Id = tagId * 100,
            TagId = tagId,
            Keyword = keyword,
            Field = field,
            Weight = weight,
            Tag = tag,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    [Fact]
    public async Task Test_Match_FindsExactKeywordInTitle()
    {
        // Arrange
        var tag = MakeTag(1, "React");
        var rule = MakeRule(1, "react", TagRuleField.Title, 0.8f, tag);
        var repoMock = new Mock<ITagRuleRepository>();
        repoMock.Setup(r => r.GetAllActiveRulesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([rule]);

        var matcher = new KeywordMatcher(repoMock.Object, DefaultOptions());
        var video = new VideoContext("Learn React", "Some description");

        // Act
        var result = await matcher.MatchAsync(video);

        // Assert
        result.Should().ContainSingle(s => s.TagId == 1);
    }

    [Fact]
    public async Task Test_Match_FindsSubstringInDescription()
    {
        // Arrange
        var tag = MakeTag(2, "Vue");
        var rule = MakeRule(2, "vue", TagRuleField.Description, 0.8f, tag);
        var repoMock = new Mock<ITagRuleRepository>();
        repoMock.Setup(r => r.GetAllActiveRulesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([rule]);

        var matcher = new KeywordMatcher(repoMock.Object, DefaultOptions());
        var video = new VideoContext("Frontend tutorial", "Building apps with Vue framework");

        // Act
        var result = await matcher.MatchAsync(video);

        // Assert
        result.Should().ContainSingle(s => s.TagId == 2);
    }

    [Fact]
    public async Task Test_Match_IsCaseInsensitive()
    {
        // Arrange
        var tag = MakeTag(1, "React");
        var rule = MakeRule(1, "REACT", TagRuleField.Title, 0.8f, tag);
        var repoMock = new Mock<ITagRuleRepository>();
        repoMock.Setup(r => r.GetAllActiveRulesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([rule]);

        var matcher = new KeywordMatcher(repoMock.Object, DefaultOptions());
        var video = new VideoContext("react tutorial", "Some description");

        // Act
        var result = await matcher.MatchAsync(video);

        // Assert
        result.Should().ContainSingle(s => s.TagId == 1);
    }

    [Fact]
    public async Task Test_Match_ReturnsWeightedScores()
    {
        // Arrange
        var tag = MakeTag(1, "React");
        var rule = MakeRule(1, "react", TagRuleField.Title, 0.8f, tag);
        var repoMock = new Mock<ITagRuleRepository>();
        repoMock.Setup(r => r.GetAllActiveRulesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([rule]);

        var matcher = new KeywordMatcher(repoMock.Object, DefaultOptions(0.5f));
        var video = new VideoContext("Learn React", "");

        // Act
        var result = await matcher.MatchAsync(video);

        // Assert
        result.Should().ContainSingle();
        result[0].Confidence.Should().BeApproximately(0.8f, 0.001f);
    }

    [Fact]
    public async Task Test_Match_AggregatesMultipleRuleWeights()
    {
        // Arrange
        var tag = MakeTag(1, "React");
        var rule1 = MakeRule(1, "react", TagRuleField.Title, 0.4f, tag);
        var rule2 = new TagRule
        {
            Id = 200,
            TagId = 1,
            Keyword = "hooks",
            Field = TagRuleField.Title,
            Weight = 0.5f,
            Tag = tag,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var repoMock = new Mock<ITagRuleRepository>();
        repoMock.Setup(r => r.GetAllActiveRulesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([rule1, rule2]);

        var matcher = new KeywordMatcher(repoMock.Object, DefaultOptions(0.7f));
        var video = new VideoContext("Learn React hooks", "");

        // Act
        var result = await matcher.MatchAsync(video);

        // Assert
        result.Should().ContainSingle();
        result[0].Confidence.Should().BeApproximately(0.9f, 0.001f);
    }

    [Fact]
    public async Task Test_Match_FieldFilter_TitleOnly_IgnoresDescription()
    {
        // Arrange
        var tag = MakeTag(1, "React");
        var rule = MakeRule(1, "react", TagRuleField.Title, 0.8f, tag);
        var repoMock = new Mock<ITagRuleRepository>();
        repoMock.Setup(r => r.GetAllActiveRulesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([rule]);

        var matcher = new KeywordMatcher(repoMock.Object, DefaultOptions());
        var video = new VideoContext("Frontend tutorial", "Learning react from scratch");

        // Act
        var result = await matcher.MatchAsync(video);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Test_Match_FieldFilter_DescriptionOnly_IgnoresTitle()
    {
        // Arrange
        var tag = MakeTag(1, "React");
        var rule = MakeRule(1, "react", TagRuleField.Description, 0.8f, tag);
        var repoMock = new Mock<ITagRuleRepository>();
        repoMock.Setup(r => r.GetAllActiveRulesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([rule]);

        var matcher = new KeywordMatcher(repoMock.Object, DefaultOptions());
        var video = new VideoContext("Learn React", "Frontend basics");

        // Act
        var result = await matcher.MatchAsync(video);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Test_Match_NoRules_ReturnsEmpty()
    {
        // Arrange
        var repoMock = new Mock<ITagRuleRepository>();
        repoMock.Setup(r => r.GetAllActiveRulesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

        var matcher = new KeywordMatcher(repoMock.Object, DefaultOptions());
        var video = new VideoContext("Learn React", "Some description");

        // Act
        var result = await matcher.MatchAsync(video);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Test_Match_BelowThreshold_ExcludesTag()
    {
        // Arrange
        var tag = MakeTag(1, "React");
        var rule = MakeRule(1, "react", TagRuleField.Title, 0.3f, tag);
        var repoMock = new Mock<ITagRuleRepository>();
        repoMock.Setup(r => r.GetAllActiveRulesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([rule]);

        var matcher = new KeywordMatcher(repoMock.Object, DefaultOptions(threshold: 0.7f));
        var video = new VideoContext("Learn React", "");

        // Act
        var result = await matcher.MatchAsync(video);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Test_Match_AboveThreshold_IncludesTag()
    {
        // Arrange
        var tag = MakeTag(1, "React");
        var rule = MakeRule(1, "react", TagRuleField.Title, 0.8f, tag);
        var repoMock = new Mock<ITagRuleRepository>();
        repoMock.Setup(r => r.GetAllActiveRulesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([rule]);

        var matcher = new KeywordMatcher(repoMock.Object, DefaultOptions(threshold: 0.7f));
        var video = new VideoContext("Learn React", "");

        // Act
        var result = await matcher.MatchAsync(video);

        // Assert
        result.Should().ContainSingle(s => s.TagId == 1 && s.Confidence >= 0.7f);
    }

    [Fact]
    public async Task Test_Match_MultipleTagsCanMatch()
    {
        // Arrange
        var tagReact = MakeTag(1, "React");
        var tagVue = MakeTag(2, "Vue");
        var rule1 = MakeRule(1, "react", TagRuleField.Both, 0.8f, tagReact);
        var rule2 = MakeRule(2, "vue", TagRuleField.Both, 0.75f, tagVue);
        var repoMock = new Mock<ITagRuleRepository>();
        repoMock.Setup(r => r.GetAllActiveRulesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([rule1, rule2]);

        var matcher = new KeywordMatcher(repoMock.Object, DefaultOptions(threshold: 0.7f));
        var video = new VideoContext("React vs Vue comparison", "Comparing react and vue frameworks");

        // Act
        var result = await matcher.MatchAsync(video);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(s => s.TagId == 1);
        result.Should().Contain(s => s.TagId == 2);
    }
}
