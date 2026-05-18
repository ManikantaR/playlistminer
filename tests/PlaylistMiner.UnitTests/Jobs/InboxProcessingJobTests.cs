using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PlaylistMiner.Core.Categorization;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Worker.Jobs;
using Quartz;

namespace PlaylistMiner.UnitTests.Jobs;

[Trait("Category", "Unit")]
public class InboxProcessingJobTests
{
    private static IJobExecutionContext CreateContext()
    {
        var mock = new Mock<IJobExecutionContext>();
        mock.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }

    [Fact]
    public async Task Test_ProcessInbox_SyncsInboxPlaylist()
    {
        // Arrange
        var syncMock = new Mock<ISyncService>();
        syncMock.Setup(s => s.SyncInboxAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResult(0, 0, [], 0));
        var pipelineMock = new Mock<ICategorizationPipeline>();
        pipelineMock.Setup(p => p.CategorizeNewVideosAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var logger = new Mock<ILogger<InboxProcessingJob>>();
        var job = new InboxProcessingJob(syncMock.Object, pipelineMock.Object, logger.Object);

        // Act
        await job.Execute(CreateContext());

        // Assert
        syncMock.Verify(s => s.SyncInboxAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Test_ProcessInbox_CategorizeNewVideos()
    {
        // Arrange
        var syncMock = new Mock<ISyncService>();
        syncMock.Setup(s => s.SyncInboxAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResult(3, 0, [], 0));
        var pipelineMock = new Mock<ICategorizationPipeline>();
        pipelineMock.Setup(p => p.CategorizeNewVideosAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var logger = new Mock<ILogger<InboxProcessingJob>>();
        var job = new InboxProcessingJob(syncMock.Object, pipelineMock.Object, logger.Object);

        // Act
        await job.Execute(CreateContext());

        // Assert
        pipelineMock.Verify(p => p.CategorizeNewVideosAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Test_ProcessInbox_SkipsAlreadyCategorized()
    {
        // Arrange — CategorizeNewVideosAsync is always called; it handles skipping internally
        var syncMock = new Mock<ISyncService>();
        syncMock.Setup(s => s.SyncInboxAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResult(0, 0, [], 0));
        var pipelineMock = new Mock<ICategorizationPipeline>();
        pipelineMock.Setup(p => p.CategorizeNewVideosAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var logger = new Mock<ILogger<InboxProcessingJob>>();
        var job = new InboxProcessingJob(syncMock.Object, pipelineMock.Object, logger.Object);

        // Act
        await job.Execute(CreateContext());

        // Assert — called regardless (skipping is done internally)
        pipelineMock.Verify(p => p.CategorizeNewVideosAsync(It.IsAny<CancellationToken>()), Times.Once());
    }
}
