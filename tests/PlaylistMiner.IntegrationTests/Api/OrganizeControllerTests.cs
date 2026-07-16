using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;

namespace PlaylistMiner.IntegrationTests.Api;

[Trait("Category", "Integration")]
public class OrganizeControllerTests
{
    [Fact]
    public async Task Test_BuildOrganizePlan_Returns200_WithPreview()
    {
        var mockPlanner = new Mock<IOrganizePlannerService>();
        var mockExecutor = new Mock<IOrganizeExecutorService>();
        mockPlanner.Setup(s => s.BuildPlanAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrganizePlanDto(
                2,
                3,
                250,
                [
                    new OrganizePlanItemDto(
                        "create_playlist",
                        null,
                        null,
                        null,
                        null,
                        "AI Agents",
                        null,
                        "AI Agents",
                        null,
                        50,
                        "Managed playlist does not exist yet."),
                    new OrganizePlanItemDto(
                        "move",
                        42,
                        "dupvideo01",
                        "Distributed Systems Deep Dive",
                        "Incoming",
                        "AI Agents",
                        7,
                        "AI Agents",
                        0.92f,
                        100,
                        "Best topic confidence is above threshold.")
                ]));

        using var factory = new PlaylistMinerWebAppFactory(services =>
        {
            services.AddSingleton(mockPlanner.Object);
            services.AddSingleton(mockExecutor.Object);
        });
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/organize/plan", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var plan = await response.Content.ReadFromJsonAsync<OrganizePlanDto>();
        plan.Should().NotBeNull();
        plan!.VideosExamined.Should().Be(2);
        plan.TotalEstimatedQuotaCost.Should().Be(250);
        plan.Items.Should().HaveCount(2);
        plan.Items[0].Action.Should().Be("create_playlist");
        plan.Items[1].Action.Should().Be("move");
    }
}
