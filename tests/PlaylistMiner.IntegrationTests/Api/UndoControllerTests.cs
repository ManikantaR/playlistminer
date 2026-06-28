using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Exceptions;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.IntegrationTests.Api;

[Trait("Category", "Integration")]
public class UndoControllerTests
{
    [Fact]
    public async Task Test_GetPending_Returns200()
    {
        // Arrange
        var mockUndo = new Mock<IUndoRepository>();
        mockUndo.Setup(r => r.GetPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new UndoLogDto(1, 1, "Test Video", 1, "Source", 2, "Target",
                    DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddDays(7), false)
            ]);

        var mockOrganizer = new Mock<IPlaylistOrganizer>();

        using var factory = new PlaylistMinerWebAppFactory(services =>
        {
            services.AddSingleton(mockUndo.Object);
            services.AddSingleton(mockOrganizer.Object);
        });
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/undo");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<UndoLogDto>>();
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task Test_GetPending_WithRealRepository_ReturnsSeededUndoLogs()
    {
        // Arrange
        using var factory = new PlaylistMinerWebAppFactory();
        var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlaylistMinerDbContext>();

            var source = new Playlist
            {
                YouTubeId = "PLsource",
                Name = "Source",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                SyncedAt = DateTime.UtcNow
            };
            var target = new Playlist
            {
                YouTubeId = "PLtarget",
                Name = "Target",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                SyncedAt = DateTime.UtcNow
            };
            var video = new Video
            {
                YouTubeId = "vid001",
                Title = "Seeded Video",
                Description = "desc",
                ChannelName = "Channel",
                ChannelId = "UC123",
                ThumbnailUrl = "https://thumb.jpg",
                Status = VideoStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                SyncedAt = DateTime.UtcNow
            };

            db.Playlists.AddRange(source, target);
            db.Videos.Add(video);
            await db.SaveChangesAsync();

            db.UndoLogs.Add(new UndoLog
            {
                VideoId = video.Id,
                Action = "move",
                SourcePlaylistId = source.Id,
                TargetPlaylistId = target.Id,
                PerformedAt = DateTime.UtcNow.AddMinutes(-2),
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                Undone = false
            });
            await db.SaveChangesAsync();
        }

        // Act
        var response = await client.GetAsync("/api/undo");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<UndoLogDto>>();
        result.Should().ContainSingle();
        result![0].VideoTitle.Should().Be("Seeded Video");
        result[0].SourcePlaylistName.Should().Be("Source");
        result[0].TargetPlaylistName.Should().Be("Target");
    }

    [Fact]
    public async Task Test_Undo_Returns200_ReversesAction()
    {
        // Arrange
        var mockUndo = new Mock<IUndoRepository>();
        var mockOrganizer = new Mock<IPlaylistOrganizer>();
        mockOrganizer.Setup(o => o.UndoMoveAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var factory = new PlaylistMinerWebAppFactory(services =>
        {
            services.AddSingleton(mockUndo.Object);
            services.AddSingleton(mockOrganizer.Object);
        });
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/undo/1", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        mockOrganizer.Verify(o => o.UndoMoveAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Test_Undo_Expired_Returns410()
    {
        // Arrange
        var mockUndo = new Mock<IUndoRepository>();
        var mockOrganizer = new Mock<IPlaylistOrganizer>();
        mockOrganizer.Setup(o => o.UndoMoveAsync(1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GoneException("UndoLog 1 has expired."));

        using var factory = new PlaylistMinerWebAppFactory(services =>
        {
            services.AddSingleton(mockUndo.Object);
            services.AddSingleton(mockOrganizer.Object);
        });
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/undo/1", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }
}
