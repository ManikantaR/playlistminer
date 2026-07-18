using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;

namespace PlaylistMiner.IntegrationTests.Api;

[Trait("Category", "Integration")]
public class AgentControllerTests
{
    [Fact]
    public async Task Test_ProcessNow_Returns200_WithProcessSummary()
    {
        // Arrange
        var agent = new Mock<IAgentProcessService>();
        agent.Setup(x => x.ProcessNowAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProcessResultDto(
                "completed",
                "Processed inbox now.",
                new SyncResult(1, 0, [], 0),
                new OrganizeExecutionResultDto(1, 1, 1, 0, 0, [], "run-now")));

        using var factory = new PlaylistMinerWebAppFactory(services =>
        {
            services.AddSingleton(agent.Object);
        });
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/agent/process-now", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AgentProcessResultDto>();
        body.Should().NotBeNull();
        body!.Status.Should().Be("completed");
        body.Execution!.RunId.Should().Be("run-now");
    }
}
