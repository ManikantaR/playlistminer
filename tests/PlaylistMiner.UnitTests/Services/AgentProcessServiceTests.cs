using FluentAssertions;
using Moq;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Infrastructure.Services;

namespace PlaylistMiner.UnitTests.Services;

[Trait("Category", "Unit")]
public class AgentProcessServiceTests
{
    [Fact]
    public async Task Test_ProcessNowAsync_WhenOllamaUnavailable_SkipsWithoutTouchingInbox()
    {
        // Arrange
        var ollama = new Mock<IOllamaCategorizer>();
        ollama.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var sync = new Mock<ISyncService>(MockBehavior.Strict);
        var pipeline = new Mock<ICategorizationPipeline>(MockBehavior.Strict);
        var executor = new Mock<IOrganizeExecutorService>(MockBehavior.Strict);
        var service = new AgentProcessService(sync.Object, pipeline.Object, executor.Object, ollama.Object);

        // Act
        var result = await service.ProcessNowAsync();

        // Assert
        result.Status.Should().Be("skipped");
        result.Message.Should().Contain("Ollama");
        result.Sync.Should().BeNull();
        result.Execution.Should().BeNull();
        sync.Verify(x => x.SyncInboxAsync(It.IsAny<CancellationToken>()), Times.Never);
        pipeline.Verify(x => x.CategorizeNewVideosAsync(It.IsAny<CancellationToken>()), Times.Never);
        executor.Verify(x => x.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Test_ProcessNowAsync_WhenOllamaAvailable_SyncsCategorizesAndExecutes()
    {
        // Arrange
        var ollama = new Mock<IOllamaCategorizer>();
        ollama.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sync = new Mock<ISyncService>();
        sync.Setup(x => x.SyncInboxAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResult(3, 0, [], 0));
        var pipeline = new Mock<ICategorizationPipeline>();
        pipeline.Setup(x => x.CategorizeNewVideosAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var executor = new Mock<IOrganizeExecutorService>();
        executor.Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrganizeExecutionResultDto(3, 2, 2, 0, 0, [], "run-8"));
        var service = new AgentProcessService(sync.Object, pipeline.Object, executor.Object, ollama.Object);

        // Act
        var result = await service.ProcessNowAsync();

        // Assert
        result.Status.Should().Be("completed");
        result.Sync.Should().NotBeNull();
        result.Execution.Should().NotBeNull();
        result.Execution!.MovesExecuted.Should().Be(2);
        sync.Verify(x => x.SyncInboxAsync(It.IsAny<CancellationToken>()), Times.Once);
        pipeline.Verify(x => x.CategorizeNewVideosAsync(It.IsAny<CancellationToken>()), Times.Once);
        executor.Verify(x => x.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
