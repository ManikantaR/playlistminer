namespace PlaylistMiner.Infrastructure.Categorization;

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PlaylistMiner.Core.Categorization;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;

public class OllamaCategorizer(HttpClient httpClient, IOptions<CategorizationOptions> options) : IOllamaCategorizer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerOptions.Default)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<TagSuggestion>> CategorizeAsync(
        VideoContext video,
        IEnumerable<string> availableTags,
        CancellationToken ct = default)
    {
        try
        {
            var model = options.Value.OllamaModel;
            var tagList = string.Join(", ", availableTags);
            var prompt = string.Join(
                Environment.NewLine,
                "Given this video:",
                $"Title: {video.Title}",
                $"Description: {video.Description}",
                "",
                $"Available tags: {tagList}",
                "",
                "Return a JSON array of matching tags with confidence scores (0-1).",
                "Format: [{\"tag\":\"TagName\",\"confidence\":0.9}]",
                "Only return the JSON array, nothing else.");

            var requestBody = new { model, prompt, stream = false };
            var json = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await httpClient.PostAsync("/api/generate", content, ct);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseBody);

            if (!doc.RootElement.TryGetProperty("response", out var responseElement))
                return [];

            var innerJson = responseElement.GetString() ?? "";
            return ParseSuggestions(innerJson);
        }
        catch
        {
            return [];
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await httpClient.GetAsync("/api/tags", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static List<TagSuggestion> ParseSuggestions(string innerJson)
    {
        try
        {
            var startIdx = innerJson.IndexOf('[');
            var endIdx = innerJson.LastIndexOf(']');
            if (startIdx < 0 || endIdx < 0 || endIdx <= startIdx)
                return [];

            var arrayJson = innerJson[startIdx..(endIdx + 1)];
            using var doc = JsonDocument.Parse(arrayJson);

            var suggestions = new List<TagSuggestion>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("tag", out var tagElement)) continue;
                if (!item.TryGetProperty("confidence", out var confElement)) continue;

                var tagName = tagElement.GetString() ?? "";
                var confidence = confElement.GetSingle();
                suggestions.Add(new TagSuggestion(0, tagName, confidence, TagSource.Ollama));
            }
            return suggestions;
        }
        catch
        {
            return [];
        }
    }
}
