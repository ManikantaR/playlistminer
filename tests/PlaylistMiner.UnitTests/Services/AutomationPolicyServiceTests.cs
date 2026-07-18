using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;
using PlaylistMiner.Infrastructure.Services;
using Xunit;

namespace PlaylistMiner.UnitTests.Services;

[Trait("Category", "Unit")]
public class AutomationPolicyServiceTests
{
    private static PlaylistMinerDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<PlaylistMinerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlaylistMinerDbContext(options);
    }

    [Fact]
    public async Task Test_GetPolicyAsync_WhenNoSettingsExist_ReturnsSupervisedDefaults()
    {
        // Arrange
        using var db = CreateDb();
        var service = new AutomationPolicyService(db);

        // Act
        var policy = await service.GetPolicyAsync();

        // Assert
        policy.Mode.Should().Be("manual");
        policy.HighConfidenceThreshold.Should().Be(0.90f);
        policy.ReviewThreshold.Should().Be(0.65f);
        policy.DailyMoveBudget.Should().Be(80);
        policy.NightlyRestoreBudget.Should().Be(150);
        policy.CleanupRecommendationCount.Should().Be(5);
        policy.OffPeakWindowStart.Should().Be("23:00");
        policy.OffPeakWindowEnd.Should().Be("05:00");
        policy.PublicAiFallbackEnabled.Should().BeFalse();
        policy.PublicAiProvider.Should().BeNull();
        policy.PublicAiModel.Should().BeNull();
        policy.TranscriptCloudPolicy.Should().Be("never");
        policy.IsPaused.Should().BeFalse();
    }

    [Fact]
    public async Task Test_UpdatePolicyAsync_WithValidPolicy_PersistsAllSettings()
    {
        // Arrange
        using var db = CreateDb();
        var service = new AutomationPolicyService(db);
        var request = new UpdateAutomationPolicyRequest(
            Mode: "first_week_approval",
            HighConfidenceThreshold: 0.88f,
            ReviewThreshold: 0.55f,
            DailyMoveBudget: 60,
            NightlyRestoreBudget: 120,
            CleanupRecommendationCount: 7,
            OffPeakWindowStart: "22:30",
            OffPeakWindowEnd: "04:45",
            PublicAiFallbackEnabled: true,
            PublicAiProvider: "openai",
            PublicAiModel: "gpt-5-mini",
            TranscriptCloudPolicy: "metadata_only",
            IsPaused: true);

        // Act
        var updated = await service.UpdatePolicyAsync(request);

        // Assert
        updated.Mode.Should().Be("first_week_approval");
        updated.HighConfidenceThreshold.Should().Be(0.88f);
        updated.ReviewThreshold.Should().Be(0.55f);
        updated.DailyMoveBudget.Should().Be(60);
        updated.NightlyRestoreBudget.Should().Be(120);
        updated.CleanupRecommendationCount.Should().Be(7);
        updated.OffPeakWindowStart.Should().Be("22:30");
        updated.OffPeakWindowEnd.Should().Be("04:45");
        updated.PublicAiFallbackEnabled.Should().BeTrue();
        updated.PublicAiProvider.Should().Be("openai");
        updated.PublicAiModel.Should().Be("gpt-5-mini");
        updated.TranscriptCloudPolicy.Should().Be("metadata_only");
        updated.IsPaused.Should().BeTrue();

        var persisted = await db.Settings.AsNoTracking().ToDictionaryAsync(setting => setting.Key);
        persisted["automation.mode"].Value.Should().Be("first_week_approval");
        persisted["automation.daily_move_budget"].Value.Should().Be("60");
        persisted["automation.public_ai_fallback_enabled"].Value.Should().Be("true");
    }

    [Fact]
    public async Task Test_UpdatePolicyAsync_WhenHighConfidenceBelowReview_RejectsPolicy()
    {
        // Arrange
        using var db = CreateDb();
        var service = new AutomationPolicyService(db);
        var request = new UpdateAutomationPolicyRequest(
            Mode: "aggressive_with_undo",
            HighConfidenceThreshold: 0.50f,
            ReviewThreshold: 0.60f,
            DailyMoveBudget: 80,
            NightlyRestoreBudget: 150,
            CleanupRecommendationCount: 5,
            OffPeakWindowStart: "23:00",
            OffPeakWindowEnd: "05:00",
            PublicAiFallbackEnabled: false,
            PublicAiProvider: null,
            PublicAiModel: null,
            TranscriptCloudPolicy: "never",
            IsPaused: false);

        // Act
        var act = async () => await service.UpdatePolicyAsync(request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*High-confidence threshold*");
    }
}
