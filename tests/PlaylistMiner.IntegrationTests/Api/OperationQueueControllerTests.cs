using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Infrastructure.Data;
using Xunit;

namespace PlaylistMiner.IntegrationTests.Api;

[Trait("Category", "Integration")]
public class OperationQueueControllerTests
{
    [Fact]
    public async Task Test_QueueOperation_WithValidPayload_ReturnsCreatedOperation()
    {
        // Arrange
        using var factory = new PlaylistMinerWebAppFactory();
        var client = factory.CreateClient();
        var request = new CreateOperationRequestDto(
            Type: "process_now",
            Source: "myinbox",
            Target: null,
            MaxItems: 20,
            QuotaEstimate: 100,
            NotBefore: null,
            AllowedWindowStart: "23:00",
            AllowedWindowEnd: "05:00");

        // Act
        var response = await client.PostAsJsonAsync("/api/operations/queue", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var queued = await response.Content.ReadFromJsonAsync<OperationRequestDto>();
        queued.Should().NotBeNull();
        queued!.Type.Should().Be("process_now");
        queued.Status.Should().Be("queued");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlaylistMinerDbContext>();
        db.OperationRequests.Should().ContainSingle();
    }

    [Fact]
    public async Task Test_GetOperations_ReturnsQueuedOperations()
    {
        // Arrange
        using var factory = new PlaylistMinerWebAppFactory();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/operations/queue", new CreateOperationRequestDto(
            Type: "full_sync",
            Source: null,
            Target: null,
            MaxItems: null,
            QuotaEstimate: 1,
            NotBefore: null,
            AllowedWindowStart: null,
            AllowedWindowEnd: null));

        // Act
        var response = await client.GetAsync("/api/operations/queue");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var operations = await response.Content.ReadFromJsonAsync<List<OperationRequestDto>>();
        operations.Should().NotBeNull();
        operations.Should().ContainSingle(o => o.Type == "full_sync");
    }

    [Fact]
    public async Task Test_CancelOperation_WhenQueued_ReturnsCanceledOperation()
    {
        // Arrange
        using var factory = new PlaylistMinerWebAppFactory();
        var client = factory.CreateClient();
        var create = await client.PostAsJsonAsync("/api/operations/queue", new CreateOperationRequestDto(
            Type: "inbox_sync",
            Source: "myinbox",
            Target: null,
            MaxItems: null,
            QuotaEstimate: 1,
            NotBefore: null,
            AllowedWindowStart: null,
            AllowedWindowEnd: null));
        var queued = await create.Content.ReadFromJsonAsync<OperationRequestDto>();

        // Act
        var response = await client.PostAsync($"/api/operations/queue/{queued!.Id}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var canceled = await response.Content.ReadFromJsonAsync<OperationRequestDto>();
        canceled.Should().NotBeNull();
        canceled!.Status.Should().Be("canceled");
    }
}
