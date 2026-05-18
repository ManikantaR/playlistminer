using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;

namespace PlaylistMiner.IntegrationTests.Api;

[Trait("Category", "Integration")]
public class TagsControllerTests
{
    private static TagWithCountDto MakeTagDto(int id = 1) =>
        new(id, $"Tag{id}", $"tag{id}", "Tools", 0);

    [Fact]
    public async Task Test_GetTags_Returns200_WithCounts()
    {
        // Arrange
        var mockRepo = new Mock<ITagRepository>();
        mockRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeTagDto(1), MakeTagDto(2)]);

        using var factory = new PlaylistMinerWebAppFactory(services =>
            services.AddSingleton(mockRepo.Object));
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/tags");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tags = await response.Content.ReadFromJsonAsync<List<TagWithCountDto>>();
        tags.Should().HaveCount(2);
    }

    [Fact]
    public async Task Test_CreateTag_Returns201_WithCreatedTag()
    {
        // Arrange
        var mockRepo = new Mock<ITagRepository>();
        mockRepo.Setup(r => r.CreateAsync(It.IsAny<Tag>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tag { Id = 10, Name = "NewTag", Slug = "newtag", CreatedAt = DateTime.UtcNow });

        using var factory = new PlaylistMinerWebAppFactory(services =>
            services.AddSingleton(mockRepo.Object));
        var client = factory.CreateClient();

        var request = new { Name = "NewTag", Category = "Tools" };

        // Act
        var response = await client.PostAsJsonAsync("/api/tags", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var tag = await response.Content.ReadFromJsonAsync<TagWithCountDto>();
        tag.Should().NotBeNull();
        tag!.Name.Should().Be("NewTag");
    }

    [Fact]
    public async Task Test_CreateTag_DuplicateName_Returns409()
    {
        // Arrange
        var mockRepo = new Mock<ITagRepository>();
        mockRepo.Setup(r => r.CreateAsync(It.IsAny<Tag>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("duplicate key value violates unique constraint ix_tags_name"));

        using var factory = new PlaylistMinerWebAppFactory(services =>
            services.AddSingleton(mockRepo.Object));
        var client = factory.CreateClient();

        var request = new { Name = "DuplicateTag" };

        // Act
        var response = await client.PostAsJsonAsync("/api/tags", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Test_DeleteTag_Returns204()
    {
        // Arrange
        var mockRepo = new Mock<ITagRepository>();
        mockRepo.Setup(r => r.DeleteAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var factory = new PlaylistMinerWebAppFactory(services =>
            services.AddSingleton(mockRepo.Object));
        var client = factory.CreateClient();

        // Act
        var response = await client.DeleteAsync("/api/tags/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Test_GetRules_Returns200()
    {
        // Arrange
        var mockRepo = new Mock<ITagRepository>();
        mockRepo.Setup(r => r.GetRulesAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TagRule { Id = 1, TagId = 1, Keyword = "react", Field = TagRuleField.Both, Weight = 0.8f, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }]);

        using var factory = new PlaylistMinerWebAppFactory(services =>
            services.AddSingleton(mockRepo.Object));
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/tags/1/rules");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rules = await response.Content.ReadFromJsonAsync<List<TagRule>>();
        rules.Should().HaveCount(1);
    }

    [Fact]
    public async Task Test_AddRule_Returns201()
    {
        // Arrange
        var mockRepo = new Mock<ITagRepository>();
        mockRepo.Setup(r => r.AddRuleAsync(It.IsAny<TagRule>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TagRule { Id = 5, TagId = 1, Keyword = "react", Field = TagRuleField.Both, Weight = 0.8f, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        using var factory = new PlaylistMinerWebAppFactory(services =>
            services.AddSingleton(mockRepo.Object));
        var client = factory.CreateClient();

        var request = new { Keyword = "react", Field = TagRuleField.Both, Weight = 0.8f };

        // Act
        var response = await client.PostAsJsonAsync("/api/tags/1/rules", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Test_DeleteRule_Returns204()
    {
        // Arrange
        var mockRepo = new Mock<ITagRepository>();
        mockRepo.Setup(r => r.DeleteRuleAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var factory = new PlaylistMinerWebAppFactory(services =>
            services.AddSingleton(mockRepo.Object));
        var client = factory.CreateClient();

        // Act
        var response = await client.DeleteAsync("/api/tags/1/rules/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
