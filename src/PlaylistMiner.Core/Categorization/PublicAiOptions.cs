namespace PlaylistMiner.Core.Categorization;

public class PublicAiOptions
{
    public const string SectionName = "PublicAI";

    public string GeminiApiKey { get; set; } = string.Empty;
    public string OpenAIApiKey { get; set; } = string.Empty;
}
