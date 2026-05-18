using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Exceptions;
using PlaylistMiner.Core.Interfaces;

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
