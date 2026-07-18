using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;

namespace PlaylistMiner.IntegrationTests.Api;

[Trait("Category", "Integration")]
public class PlaylistsControllerTests
{
    private static PlaylistDto MakePlaylistDto(int id = 1) =>
        new($"PL{id:D4}", $"Playlist {id}", null, false, 0, id);

    [Fact]
    public async Task Test_GetPlaylists_Returns200()
    {
        // Arrange
        var mockRepo = new Mock<IPlaylistRepository>();
        mockRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakePlaylistDto(1), MakePlaylistDto(2)]);

        var mockOrganizer = new Mock<IPlaylistOrganizer>();

        using var factory = new PlaylistMinerWebAppFactory(services =>
        {
            services.AddSingleton(mockRepo.Object);
            services.AddSingleton(mockOrganizer.Object);
        });
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/playlists");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var playlists = await response.Content.ReadFromJsonAsync<List<PlaylistDto>>();
        playlists.Should().HaveCount(2);
    }

    [Fact]
    public async Task Test_CreatePlaylist_Returns201()
    {
        // Arrange
        var mockRepo = new Mock<IPlaylistRepository>();
        mockRepo.Setup(r => r.CreateAsync(It.IsAny<Playlist>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Playlist
            {
                Id = 10,
                YouTubeId = "PL0001",
                Name = "New Playlist",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                SyncedAt = DateTime.UtcNow
            });

        var mockOrganizer = new Mock<IPlaylistOrganizer>();

        using var factory = new PlaylistMinerWebAppFactory(services =>
        {
            services.AddSingleton(mockRepo.Object);
            services.AddSingleton(mockOrganizer.Object);
        });
        var client = factory.CreateClient();

        var request = new { Title = "New Playlist", Description = "A new playlist" };

        // Act
        var response = await client.PostAsJsonAsync("/api/playlists", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Test_SetInbox_Returns200_ClearsPrevious()
    {
        // Arrange
        var mockRepo = new Mock<IPlaylistRepository>();
        mockRepo.Setup(r => r.SetInboxAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockOrganizer = new Mock<IPlaylistOrganizer>();

        using var factory = new PlaylistMinerWebAppFactory(services =>
        {
            services.AddSingleton(mockRepo.Object);
            services.AddSingleton(mockOrganizer.Object);
        });
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/playlists/1/inbox", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        mockRepo.Verify(r => r.SetInboxAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Test_SetInbox_WhenPlaylistMissing_Returns404ProblemDetails()
    {
        // Arrange
        var mockRepo = new Mock<IPlaylistRepository>();
        mockRepo.Setup(r => r.SetInboxAsync(999, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Playlist with id 999 was not found."));

        var mockOrganizer = new Mock<IPlaylistOrganizer>();

        using var factory = new PlaylistMinerWebAppFactory(services =>
        {
            services.AddSingleton(mockRepo.Object);
            services.AddSingleton(mockOrganizer.Object);
        });
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/playlists/999/set-inbox", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var details = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        details.Should().NotBeNull();
        details!.Title.Should().Be("Playlist not found.");
        details.Detail.Should().Contain("999");
    }

    [Fact]
    public async Task Test_Consolidate_Returns200_WithResult()
    {
        // Arrange
        var mockRepo = new Mock<IPlaylistRepository>();
        var mockOrganizer = new Mock<IPlaylistOrganizer>();
        mockOrganizer.Setup(o => o.ConsolidateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakePlaylistDto(1), MakePlaylistDto(2)]);

        using var factory = new PlaylistMinerWebAppFactory(services =>
        {
            services.AddSingleton(mockRepo.Object);
            services.AddSingleton(mockOrganizer.Object);
        });
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/playlists/consolidate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<PlaylistDto>>();
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Test_RestoreSample_Returns200_WithResult()
    {
        // Arrange
        var mockRepo = new Mock<IPlaylistRepository>();
        var mockOrganizer = new Mock<IPlaylistOrganizer>();
        var mockRestore = new Mock<IPlaylistRestoreService>();
        mockRestore.Setup(s => s.RestoreSampleAsync(6, 409, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaylistRestoreResultDto(
                6,
                409,
                5,
                1,
                0,
                [new PlaylistRestoreItemDto(11897, "qNq4GDZ0-VU", "Foundation Models", 1, 67, "pli-new")]));

        using var factory = new PlaylistMinerWebAppFactory(services =>
        {
            services.AddSingleton(mockRepo.Object);
            services.AddSingleton(mockOrganizer.Object);
            services.AddSingleton(mockRestore.Object);
        });
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/playlists/409/restore-sample",
            new { SourcePlaylistId = 6, MaxCount = 5 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PlaylistRestoreResultDto>();
        result.Should().NotBeNull();
        result!.AddedCount.Should().Be(1);
        result.Added.Should().ContainSingle(i => i.YouTubeId == "qNq4GDZ0-VU");
    }
}
