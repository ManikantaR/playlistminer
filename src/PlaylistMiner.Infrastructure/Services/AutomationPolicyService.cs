using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.Infrastructure.Services;

public class AutomationPolicyService(PlaylistMinerDbContext db) : IAutomationPolicyService
{
    private const string KeyPrefix = "automation.";

    private static readonly HashSet<string> AllowedModes =
    [
        "manual",
        "first_week_approval",
        "aggressive_with_undo"
    ];

    private static readonly HashSet<string> AllowedTranscriptCloudPolicies =
    [
        "never",
        "metadata_only",
        "allow_transcripts"
    ];

    private static readonly HashSet<string> AllowedPublicAiProviders =
    [
        "openai",
        "gemini"
    ];

    public async Task<AutomationPolicyDto> GetPolicyAsync(CancellationToken ct = default)
    {
        var settings = await db.Settings
            .AsNoTracking()
            .Where(setting => setting.Key.StartsWith(KeyPrefix))
            .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, ct);

        return new AutomationPolicyDto(
            Mode: GetString(settings, "mode", "manual"),
            HighConfidenceThreshold: GetFloat(settings, "high_confidence_threshold", 0.90f),
            ReviewThreshold: GetFloat(settings, "review_threshold", 0.65f),
            DailyMoveBudget: GetInt(settings, "daily_move_budget", 80),
            NightlyRestoreBudget: GetInt(settings, "nightly_restore_budget", 150),
            CleanupRecommendationCount: GetInt(settings, "cleanup_recommendation_count", 5),
            OffPeakWindowStart: GetString(settings, "off_peak_window_start", "23:00"),
            OffPeakWindowEnd: GetString(settings, "off_peak_window_end", "05:00"),
            PublicAiFallbackEnabled: GetBool(settings, "public_ai_fallback_enabled", false),
            PublicAiProvider: GetNullableString(settings, "public_ai_provider"),
            PublicAiModel: GetNullableString(settings, "public_ai_model"),
            TranscriptCloudPolicy: GetString(settings, "transcript_cloud_policy", "never"),
            IsPaused: GetBool(settings, "is_paused", false));
    }

    public async Task<AutomationPolicyDto> UpdatePolicyAsync(
        UpdateAutomationPolicyRequest request,
        CancellationToken ct = default)
    {
        Validate(request);

        var values = new Dictionary<string, string>
        {
            ["mode"] = request.Mode,
            ["high_confidence_threshold"] = FormatFloat(request.HighConfidenceThreshold),
            ["review_threshold"] = FormatFloat(request.ReviewThreshold),
            ["daily_move_budget"] = request.DailyMoveBudget.ToString(CultureInfo.InvariantCulture),
            ["nightly_restore_budget"] = request.NightlyRestoreBudget.ToString(CultureInfo.InvariantCulture),
            ["cleanup_recommendation_count"] = request.CleanupRecommendationCount.ToString(CultureInfo.InvariantCulture),
            ["off_peak_window_start"] = request.OffPeakWindowStart,
            ["off_peak_window_end"] = request.OffPeakWindowEnd,
            ["public_ai_fallback_enabled"] = request.PublicAiFallbackEnabled.ToString().ToLowerInvariant(),
            ["public_ai_provider"] = request.PublicAiProvider?.Trim() ?? string.Empty,
            ["public_ai_model"] = request.PublicAiModel?.Trim() ?? string.Empty,
            ["transcript_cloud_policy"] = request.TranscriptCloudPolicy,
            ["is_paused"] = request.IsPaused.ToString().ToLowerInvariant()
        };

        var now = DateTime.UtcNow;
        foreach (var (shortKey, value) in values)
        {
            await UpsertAsync($"{KeyPrefix}{shortKey}", value, now, ct);
        }

        await db.SaveChangesAsync(ct);
        return await GetPolicyAsync(ct);
    }

    private async Task UpsertAsync(string key, string value, DateTime updatedAt, CancellationToken ct)
    {
        var existing = await db.Settings.FindAsync([key], ct);
        if (existing is null)
        {
            db.Settings.Add(new Setting
            {
                Key = key,
                Value = value,
                UpdatedAt = updatedAt
            });
            return;
        }

        existing.Value = value;
        existing.UpdatedAt = updatedAt;
    }

    private static void Validate(UpdateAutomationPolicyRequest request)
    {
        if (!AllowedModes.Contains(request.Mode))
        {
            throw new ArgumentException("Unsupported automation mode.");
        }

        ValidateRatio(request.HighConfidenceThreshold, "High-confidence threshold");
        ValidateRatio(request.ReviewThreshold, "Review threshold");

        if (request.HighConfidenceThreshold < request.ReviewThreshold)
        {
            throw new ArgumentException("High-confidence threshold must be greater than or equal to review threshold.");
        }

        if (request.DailyMoveBudget is < 0 or > 500)
        {
            throw new ArgumentException("Daily move budget must be between 0 and 500.");
        }

        if (request.NightlyRestoreBudget is < 0 or > 500)
        {
            throw new ArgumentException("Nightly restore budget must be between 0 and 500.");
        }

        if (request.CleanupRecommendationCount is < 1 or > 25)
        {
            throw new ArgumentException("Cleanup recommendation count must be between 1 and 25.");
        }

        ValidateTime(request.OffPeakWindowStart, "Off-peak window start");
        ValidateTime(request.OffPeakWindowEnd, "Off-peak window end");

        if (!AllowedTranscriptCloudPolicies.Contains(request.TranscriptCloudPolicy))
        {
            throw new ArgumentException("Unsupported transcript cloud policy.");
        }

        if (!request.PublicAiFallbackEnabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(request.PublicAiProvider))
        {
            throw new ArgumentException("Public AI provider is required when fallback is enabled.");
        }

        if (!AllowedPublicAiProviders.Contains(request.PublicAiProvider.Trim().ToLowerInvariant()))
        {
            throw new ArgumentException("Unsupported public AI provider.");
        }

        if (string.IsNullOrWhiteSpace(request.PublicAiModel))
        {
            throw new ArgumentException("Public AI model is required when fallback is enabled.");
        }

        if (request.PublicAiModel.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("Public AI model must be an API model id, not a display label.");
        }
    }

    private static void ValidateRatio(float value, string label)
    {
        if (value is < 0f or > 1f)
        {
            throw new ArgumentException($"{label} must be between 0 and 1.");
        }
    }

    private static void ValidateTime(string value, string label)
    {
        if (!TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            throw new ArgumentException($"{label} must use HH:mm format.");
        }
    }

    private static string GetString(Dictionary<string, string> settings, string key, string defaultValue)
        => settings.GetValueOrDefault($"{KeyPrefix}{key}") ?? defaultValue;

    private static string? GetNullableString(Dictionary<string, string> settings, string key)
    {
        var value = settings.GetValueOrDefault($"{KeyPrefix}{key}");
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int GetInt(Dictionary<string, string> settings, string key, int defaultValue)
        => int.TryParse(GetString(settings, key, string.Empty), CultureInfo.InvariantCulture, out var value)
            ? value
            : defaultValue;

    private static float GetFloat(Dictionary<string, string> settings, string key, float defaultValue)
        => float.TryParse(GetString(settings, key, string.Empty), CultureInfo.InvariantCulture, out var value)
            ? value
            : defaultValue;

    private static bool GetBool(Dictionary<string, string> settings, string key, bool defaultValue)
        => bool.TryParse(GetString(settings, key, string.Empty), out var value)
            ? value
            : defaultValue;

    private static string FormatFloat(float value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);
}
