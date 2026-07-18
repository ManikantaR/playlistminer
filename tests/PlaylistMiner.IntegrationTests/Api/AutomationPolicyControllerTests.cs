using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Infrastructure.Data;
using Xunit;

namespace PlaylistMiner.IntegrationTests.Api;

[Trait("Category", "Integration")]
public class AutomationPolicyControllerTests
{
    [Fact]
    public async Task Test_GetPolicy_ReturnsDefaultAutomationPolicy()
    {
        // Arrange
        using var factory = new PlaylistMinerWebAppFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/automation/policy");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var policy = await response.Content.ReadFromJsonAsync<AutomationPolicyDto>();
        policy.Should().NotBeNull();
        policy!.Mode.Should().Be("manual");
        policy.OffPeakWindowStart.Should().Be("23:00");
        policy.OffPeakWindowEnd.Should().Be("05:00");
        policy.PublicAiFallbackEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Test_UpdatePolicy_WithValidPayload_PersistsPolicy()
    {
        // Arrange
        using var factory = new PlaylistMinerWebAppFactory();
        var client = factory.CreateClient();
        var request = new UpdateAutomationPolicyRequest(
            Mode: "first_week_approval",
            HighConfidenceThreshold: 0.92f,
            ReviewThreshold: 0.70f,
            DailyMoveBudget: 50,
            NightlyRestoreBudget: 125,
            CleanupRecommendationCount: 5,
            OffPeakWindowStart: "23:15",
            OffPeakWindowEnd: "04:30",
            PublicAiFallbackEnabled: true,
            PublicAiProvider: "gemini",
            PublicAiModel: "gemini-2.5-flash",
            TranscriptCloudPolicy: "metadata_only",
            IsPaused: false);

        // Act
        var response = await client.PutAsJsonAsync("/api/automation/policy", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var policy = await response.Content.ReadFromJsonAsync<AutomationPolicyDto>();
        policy.Should().NotBeNull();
        policy!.Mode.Should().Be("first_week_approval");
        policy.PublicAiProvider.Should().Be("gemini");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlaylistMinerDbContext>();
        var persisted = await db.Settings.FindAsync("automation.mode");
        persisted!.Value.Should().Be("first_week_approval");
    }

    [Fact]
    public async Task Test_UpdatePolicy_WithInvalidMode_ReturnsProblemDetails()
    {
        // Arrange
        using var factory = new PlaylistMinerWebAppFactory();
        var client = factory.CreateClient();
        var request = new UpdateAutomationPolicyRequest(
            Mode: "surprise_me",
            HighConfidenceThreshold: 0.92f,
            ReviewThreshold: 0.70f,
            DailyMoveBudget: 50,
            NightlyRestoreBudget: 125,
            CleanupRecommendationCount: 5,
            OffPeakWindowStart: "23:15",
            OffPeakWindowEnd: "04:30",
            PublicAiFallbackEnabled: false,
            PublicAiProvider: null,
            PublicAiModel: null,
            TranscriptCloudPolicy: "never",
            IsPaused: false);

        // Act
        var response = await client.PutAsJsonAsync("/api/automation/policy", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Unsupported automation mode");
    }
}
