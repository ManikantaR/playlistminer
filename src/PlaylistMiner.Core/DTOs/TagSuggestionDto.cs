using PlaylistMiner.Core.Models;

namespace PlaylistMiner.Core.DTOs;

public record TagSuggestionDto(
    int TagId,
    string TagName,
    TagSource Source,
    float? Confidence,
    string? Provider = null,
    string? ProviderModel = null);
