using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Worker;

namespace PlaylistMiner.UnitTests.Services;

[Trait("Category", "Unit")]
public class OperationQueueHostedServiceTests
{
    [Fact]
    public async Task Test_ExecuteOperationAsync_WhenPlaylistRestore_UsesOperationPayload()
    {
        var restore = new Mock<IPlaylistRestoreService>(MockBehavior.Strict);
        restore.Setup(s => s.RestoreBatchAsync(6, 409, 150, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaylistRestoreResultDto(6, 409, 150, 12, 72, []));

        var services = new ServiceCollection()
            .AddSingleton(restore.Object)
            .BuildServiceProvider();

        var operation = new OperationRequest
        {
            Type = "playlist_restore",
            Status = "running",
            CreatedBy = "user",
            Source = "6",
            Target = "409",
            MaxItems = 150,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var runId = await OperationQueueHostedService.ExecuteOperationAsync(services, operation, CancellationToken.None);

        runId.Should().Be("playlist_restore:12");
        restore.VerifyAll();
    }
}
