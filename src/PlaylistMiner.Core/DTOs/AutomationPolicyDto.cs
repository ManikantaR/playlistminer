namespace PlaylistMiner.Core.DTOs;

public record AutomationPolicyDto(
    string Mode,
    float HighConfidenceThreshold,
    float ReviewThreshold,
    int DailyMoveBudget,
    int NightlyRestoreBudget,
    int CleanupRecommendationCount,
    string OffPeakWindowStart,
    string OffPeakWindowEnd,
    bool PublicAiFallbackEnabled,
    string? PublicAiProvider,
    string? PublicAiModel,
    string TranscriptCloudPolicy,
    bool IsPaused);
