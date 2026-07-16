using Microsoft.Extensions.Logging;
using Moq;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Worker.Jobs;
using Quartz;

namespace PlaylistMiner.UnitTests.Jobs;

[Trait("Category", "Unit")]
public class OrganizeExecutionJobTests
{
    private static IJobExecutionContext CreateContext()
    {
        var mock = new Mock<IJobExecutionContext>();
        mock.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }

    [Fact]
    public async Task Test_Execute_WhenOllamaAvailable_RunsOrganizeExecutor()
    {
        var ollama = new Mock<IOllamaCategorizer>();
        ollama.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var executor = new Mock<IOrganizeExecutorService>();
        executor.Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaylistMiner.Core.DTOs.OrganizeExecutionResultDto(0, 0, 0, 0, 0, [], "run-1"));

        var logger = new Mock<ILogger<OrganizeExecutionJob>>();
        var job = new OrganizeExecutionJob(ollama.Object, executor.Object, logger.Object);

        await job.Execute(CreateContext());

        executor.Verify(x => x.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Test_Execute_WhenOllamaUnavailable_SkipsOrganizeExecutor()
    {
        var ollama = new Mock<IOllamaCategorizer>();
        ollama.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var executor = new Mock<IOrganizeExecutorService>(MockBehavior.Strict);
        var logger = new Mock<ILogger<OrganizeExecutionJob>>();
        var job = new OrganizeExecutionJob(ollama.Object, executor.Object, logger.Object);

        await job.Execute(CreateContext());

        executor.Verify(x => x.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
