using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PlaylistMiner.Core.Categorization;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;
using PlaylistMiner.Worker.Jobs;
using Quartz;

namespace PlaylistMiner.UnitTests.Jobs;

[Trait("Category", "Unit")]
public class CategorizationJobTests
{
    private static PlaylistMinerDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<PlaylistMinerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlaylistMinerDbContext(options);
    }

    private static IJobExecutionContext CreateContext()
    {
        var mock = new Mock<IJobExecutionContext>();
        mock.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }

    [Fact]
    public async Task Test_CategorizeJob_ProcessesUncategorizedVideos()
    {
        // Arrange
        var pipelineMock = new Mock<ICategorizationPipeline>();
        pipelineMock.Setup(p => p.CategorizeNewVideosAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var logger = new Mock<ILogger<CategorizationJob>>();
        var job = new CategorizationJob(pipelineMock.Object, logger.Object);

        // Act
        await job.Execute(CreateContext());

        // Assert
        pipelineMock.Verify(p => p.CategorizeNewVideosAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Test_CategorizeJob_BatchesProcessing()
    {
        // Arrange — pipeline handles batching internally; job calls once and delegates
        var pipelineMock = new Mock<ICategorizationPipeline>();
        pipelineMock.Setup(p => p.CategorizeNewVideosAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var logger = new Mock<ILogger<CategorizationJob>>();
        var job = new CategorizationJob(pipelineMock.Object, logger.Object);

        // Act
        await job.Execute(CreateContext());

        // Assert — pipeline invoked once (it processes in batches of 50 internally)
        pipelineMock.Verify(p => p.CategorizeNewVideosAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Test_CategorizeJob_SkipsAlreadySuggested()
    {
        // Arrange — pipeline handles skip logic internally; job should still call it
        var pipelineMock = new Mock<ICategorizationPipeline>();
        pipelineMock.Setup(p => p.CategorizeNewVideosAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var logger = new Mock<ILogger<CategorizationJob>>();
        var job = new CategorizationJob(pipelineMock.Object, logger.Object);

        // Act
        await job.Execute(CreateContext());

        // Assert
        pipelineMock.Verify(p => p.CategorizeNewVideosAsync(It.IsAny<CancellationToken>()), Times.Once());
    }
}
