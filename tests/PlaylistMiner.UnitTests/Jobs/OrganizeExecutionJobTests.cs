using Microsoft.Extensions.Logging;
using Moq;
using PlaylistMiner.Core.DTOs;
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

    private static IAutomationPolicyService CreatePolicyService(string mode = "aggressive_with_undo", bool isPaused = false)
    {
        var policy = new AutomationPolicyDto(
            mode,
            0.9f,
            0.65f,
            80,
            150,
            5,
            "23:00",
            "05:00",
            false,
            null,
            null,
            "never",
            isPaused);
        var automationPolicy = new Mock<IAutomationPolicyService>();
        automationPolicy.Setup(x => x.GetPolicyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);
        return automationPolicy.Object;
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
        var job = new OrganizeExecutionJob(ollama.Object, executor.Object, CreatePolicyService(), logger.Object);

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
        var job = new OrganizeExecutionJob(ollama.Object, executor.Object, CreatePolicyService(), logger.Object);

        await job.Execute(CreateContext());

        executor.Verify(x => x.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Test_Execute_WhenAutomationPaused_SkipsOrganizeExecutor()
    {
        var ollama = new Mock<IOllamaCategorizer>();
        ollama.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var executor = new Mock<IOrganizeExecutorService>(MockBehavior.Strict);
        var logger = new Mock<ILogger<OrganizeExecutionJob>>();
        var job = new OrganizeExecutionJob(ollama.Object, executor.Object, CreatePolicyService(isPaused: true), logger.Object);

        await job.Execute(CreateContext());

        executor.Verify(x => x.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Test_Execute_WhenModeRequiresApproval_SkipsOrganizeExecutor()
    {
        var ollama = new Mock<IOllamaCategorizer>();
        ollama.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var executor = new Mock<IOrganizeExecutorService>(MockBehavior.Strict);
        var logger = new Mock<ILogger<OrganizeExecutionJob>>();
        var job = new OrganizeExecutionJob(ollama.Object, executor.Object, CreatePolicyService("first_week_approval"), logger.Object);

        await job.Execute(CreateContext());

        executor.Verify(x => x.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
