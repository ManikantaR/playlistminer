using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;

namespace PlaylistMiner.IntegrationTests.Api;

[Trait("Category", "Integration")]
public class VideosControllerTests
{
    private static VideoDto MakeVideoDto(int id = 1) => new(
        id, $"vid{id:D3}", $"Video {id}", "Channel", "https://thumb.jpg",
        TimeSpan.FromMinutes(5), DateTime.UtcNow, VideoStatus.Active, []);

    private static PagedResult<VideoDto> MakePagedResult(int count = 1) =>
        new([.. Enumerable.Range(1, count).Select(MakeVideoDto)], count, 1, 20,
            (int)Math.Ceiling(count / 20.0));

    [Fact]
    public async Task Test_GetVideos_Returns200_WithPaginatedList()
    {
        // Arrange
        var mockRepo = new Mock<IVideoRepository>();
        mockRepo.Setup(r => r.GetAllAsync(It.IsAny<VideoFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePagedResult(5));

        using var factory = new PlaylistMinerWebAppFactory(services =>
            services.AddSingleton(mockRepo.Object));
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/videos");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<VideoDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(5);
    }

    [Fact]
    public async Task Test_GetVideos_WithSearch_ReturnsFuzzyMatches()
    {
        // Arrange
        var mockRepo = new Mock<IVideoRepository>();
        mockRepo.Setup(r => r.GetAllAsync(
                It.Is<VideoFilter>(f => f.Search == "react"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePagedResult(2));

        using var factory = new PlaylistMinerWebAppFactory(services =>
            services.AddSingleton(mockRepo.Object));
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/videos?search=react");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<VideoDto>>();
        result!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Test_GetVideos_WithTags_ReturnsFiltered()
    {
        // Arrange
        var mockRepo = new Mock<IVideoRepository>();
        mockRepo.Setup(r => r.GetAllAsync(
                It.Is<VideoFilter>(f => f.Tags != null && f.Tags.Contains(1) && f.Tags.Contains(2)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePagedResult(1));

        using var factory = new PlaylistMinerWebAppFactory(services =>
            services.AddSingleton(mockRepo.Object));
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/videos?tags=1,2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<VideoDto>>();
        result!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Test_GetVideo_Returns200_WithDetails()
    {
        // Arrange
        var detail = new VideoDetailDto(
            1, "vid001", "Video 1", "Desc", "Channel", "UC1",
            "https://thumb.jpg", TimeSpan.FromMinutes(5),
            DateTime.UtcNow, VideoStatus.Active, [], []);

        var mockRepo = new Mock<IVideoRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        using var factory = new PlaylistMinerWebAppFactory(services =>
            services.AddSingleton(mockRepo.Object));
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/videos/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VideoDetailDto>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
    }

    [Fact]
    public async Task Test_GetVideo_NotFound_Returns404()
    {
        // Arrange
        var mockRepo = new Mock<IVideoRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VideoDetailDto?)null);

        using var factory = new PlaylistMinerWebAppFactory(services =>
            services.AddSingleton(mockRepo.Object));
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/videos/999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Test_PatchTags_Returns200_UpdatesTags()
    {
        // Arrange
        var mockRepo = new Mock<IVideoRepository>();
        var mockSvc = new Mock<IVideoService>();

        using var factory = new PlaylistMinerWebAppFactory(services =>
        {
            services.AddSingleton(mockRepo.Object);
            services.AddSingleton(mockSvc.Object);
        });
        var client = factory.CreateClient();

        var request = new { TagIdsToAdd = new[] { 1, 2 }, TagIdsToRemove = new[] { 3 } };

        // Act
        var response = await client.PatchAsJsonAsync("/api/videos/1/tags", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        mockSvc.Verify(s => s.AddTagAsync(1, 1, It.IsAny<CancellationToken>()), Times.Once);
        mockSvc.Verify(s => s.AddTagAsync(1, 2, It.IsAny<CancellationToken>()), Times.Once);
        mockSvc.Verify(s => s.RemoveTagAsync(1, 3, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Test_AcceptSuggestions_Returns200_CallsSelfLearning()
    {
        // Arrange
        var mockRepo = new Mock<IVideoRepository>();
        var mockSvc = new Mock<IVideoService>();

        using var factory = new PlaylistMinerWebAppFactory(services =>
        {
            services.AddSingleton(mockRepo.Object);
            services.AddSingleton(mockSvc.Object);
        });
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/videos/1/suggestions/1/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        mockSvc.Verify(s => s.AcceptTagAsync(1, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Test_RejectSuggestions_Returns200_CallsSelfLearning()
    {
        // Arrange
        var mockRepo = new Mock<IVideoRepository>();
        var mockSvc = new Mock<IVideoService>();

        using var factory = new PlaylistMinerWebAppFactory(services =>
        {
            services.AddSingleton(mockRepo.Object);
            services.AddSingleton(mockSvc.Object);
        });
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/videos/1/suggestions/1/reject", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        mockSvc.Verify(s => s.RejectTagAsync(1, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Test_GetSuggestions_Returns200_WithPendingVideos()
    {
        // Arrange
        var mockRepo = new Mock<IVideoRepository>();
        mockRepo.Setup(r => r.GetAllAsync(It.IsAny<VideoFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePagedResult(3));

        using var factory = new PlaylistMinerWebAppFactory(services =>
            services.AddSingleton(mockRepo.Object));
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/videos/suggestions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
