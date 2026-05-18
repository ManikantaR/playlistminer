namespace PlaylistMiner.Core.Categorization;

using PlaylistMiner.Core.Models;

public record TagSuggestion(int TagId, string TagName, float Confidence, TagSource Source);
