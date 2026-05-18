using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Worker.Jobs;
using Quartz;

namespace PlaylistMiner.UnitTests.Jobs;

[Trait("Category", "Unit")]
public class SyncJobTests
{
    private static IJobExecutionContext CreateContext(CancellationToken ct = default)
    {
        var mock = new Mock<IJobExecutionContext>();
        mock.SetupGet(c => c.CancellationToken).Returns(ct);
        return mock.Object;
    }

    [Fact]
    public async Task Test_SyncJob_ExecutesFullSync()
    {
        // Arrange
        var syncMock = new Mock<ISyncService>();
        syncMock.Setup(s => s.FullSyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResult(0, 0, [], 0));
        var logger = new Mock<ILogger<SyncJob>>();
        var job = new SyncJob(syncMock.Object, logger.Object);

        // Act
        await job.Execute(CreateContext());

        // Assert
        syncMock.Verify(s => s.FullSyncAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Test_SyncJob_LogsSyncResult()
    {
        // Arrange
        var syncMock = new Mock<ISyncService>();
        syncMock.Setup(s => s.FullSyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResult(42, 10, [], 0));
        var loggerMock = new Mock<ILogger<SyncJob>>();
        var job = new SyncJob(syncMock.Object, loggerMock.Object);

        // Act
        await job.Execute(CreateContext());

        // Assert — logger.LogInformation was called (any message containing the count)
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("42")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce());
    }

    [Fact]
    public async Task Test_SyncJob_OnError_LogsAndContinues()
    {
        // Arrange
        var syncMock = new Mock<ISyncService>();
        syncMock.Setup(s => s.FullSyncAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var loggerMock = new Mock<ILogger<SyncJob>>();
        var job = new SyncJob(syncMock.Object, loggerMock.Object);

        // Act — must not throw
        var act = async () => await job.Execute(CreateContext());

        // Assert
        await act.Should().NotThrowAsync();
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once());
    }

    [Fact]
    public async Task Test_SyncJob_RespectsCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var syncMock = new Mock<ISyncService>();
        syncMock.Setup(s => s.FullSyncAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        var loggerMock = new Mock<ILogger<SyncJob>>();
        var job = new SyncJob(syncMock.Object, loggerMock.Object);

        // Act — must not throw even on cancellation
        var act = async () => await job.Execute(CreateContext(cts.Token));

        // Assert
        await act.Should().NotThrowAsync();
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once());
    }
}
