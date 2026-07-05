namespace PlaylistMiner.Core.Categorization;

public class CategorizationOptions
{
    public const string SectionName = "Categorization";
    public float AutoFileConfidence { get; set; } = 0.65f;
    public float KeywordThreshold { get; set; } = 0.7f;
    public float TfIdfThreshold { get; set; } = 0.5f;
    public string OllamaBaseUrl { get; set; } = "http://pm-ollama:11434";
    public string OllamaModel { get; set; } = "mistral";
}
