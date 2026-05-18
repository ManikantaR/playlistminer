using PlaylistMiner.Core.Models;

namespace PlaylistMiner.Api.Models;

public record AddRuleRequest(string Keyword, TagRuleField Field, float Weight);
