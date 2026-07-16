using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;

namespace PlaylistMiner.IntegrationTests.Api;

[Trait("Category", "Integration")]
public class OrganizeExecuteControllerTests
{
    [Fact]
    public async Task Test_ExecuteOrganize_Returns200_WithExecutionSummary()
    {
        var executor = new Mock<IOrganizeExecutorService>();
        executor.Setup(x => x.ExecuteAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrganizeExecutionResultDto(
                3,
                2,
                2,
                0,
                0,
                [],
                "run-123"));

        using var factory = new PlaylistMinerWebAppFactory(services =>
        {
            services.AddSingleton(executor.Object);
        });
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/organize/execute", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<OrganizeExecutionResultDto>();
        body.Should().NotBeNull();
        body!.MovesExecuted.Should().Be(2);
        body.RunId.Should().Be("run-123");
    }
}
