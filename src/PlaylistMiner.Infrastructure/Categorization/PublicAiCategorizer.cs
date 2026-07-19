namespace PlaylistMiner.Infrastructure.Categorization;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlaylistMiner.Core.Categorization;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;

public class PublicAiCategorizer(
    HttpClient httpClient,
    IOptions<PublicAiOptions> options,
    ILogger<PublicAiCategorizer> logger) : IPublicAiCategorizer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerOptions.Default)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<TagSuggestion>> CategorizeAsync(
        VideoContext video,
        IEnumerable<string> availableTags,
        AutomationPolicyDto policy,
        CancellationToken ct = default)
    {
        if (!policy.PublicAiFallbackEnabled
            || string.IsNullOrWhiteSpace(policy.PublicAiProvider)
            || string.IsNullOrWhiteSpace(policy.PublicAiModel))
        {
            return [];
        }

        var provider = policy.PublicAiProvider.Trim().ToLowerInvariant();
        var model = policy.PublicAiModel.Trim();
        var allowed = availableTags.ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (allowed.Count == 0)
        {
            return [];
        }

        try
        {
            var prompt = BuildPrompt(video, allowed, policy.TranscriptCloudPolicy);
            var content = provider switch
            {
                "gemini" => await GenerateGeminiAsync(prompt, model, ct),
                "openai" => await GenerateOpenAiAsync(prompt, model, ct),
                _ => null
            };

            if (string.IsNullOrWhiteSpace(content))
            {
                return [];
            }

            var source = provider == "openai" ? TagSource.OpenAI : TagSource.Gemini;
            return ParseSuggestions(content, allowed, source, provider, model);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Public AI categorization failed for provider {Provider}.", provider);
            return [];
        }
    }

    private async Task<string?> GenerateGeminiAsync(string prompt, string model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.Value.GeminiApiKey))
        {
            return null;
        }

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(options.Value.GeminiApiKey)}")
        {
            Content = JsonContent(requestBody)
        };

        using var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseBody);

        return doc.RootElement
            .GetProperty("candidates")
            .EnumerateArray()
            .SelectMany(candidate => candidate.GetProperty("content").GetProperty("parts").EnumerateArray())
            .Select(part => part.TryGetProperty("text", out var text) ? text.GetString() : null)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
    }

    private async Task<string?> GenerateOpenAiAsync(string prompt, string model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.Value.OpenAIApiKey))
        {
            return null;
        }

        var requestBody = new
        {
            model,
            temperature = 0,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = prompt
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = JsonContent(requestBody)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.OpenAIApiKey);

        using var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseBody);

        return doc.RootElement
            .GetProperty("choices")
            .EnumerateArray()
            .Select(choice => choice.GetProperty("message").GetProperty("content").GetString())
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
    }

    private static StringContent JsonContent(object value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static string BuildPrompt(
        VideoContext video,
        IEnumerable<string> availableTags,
        string transcriptCloudPolicy)
    {
        var tagList = string.Join(", ", availableTags.Order(StringComparer.OrdinalIgnoreCase));
        var prompt = string.Join(
            Environment.NewLine,
            "Given this YouTube video metadata:",
            $"Title: {video.Title}",
            $"Description: {video.Description}",
            "",
            $"Available tags: {tagList}",
            "",
            "Return a JSON array of matching tags with confidence scores from 0 to 1.",
            "Format: [{\"tag\":\"TagName\",\"confidence\":0.9}]",
            "Only return tags from the available tag list.",
            "Only return the JSON array, nothing else.");

        return transcriptCloudPolicy == "allow_transcripts"
            ? prompt + Environment.NewLine + "Transcript: unavailable in this request."
            : prompt;
    }

    private static List<TagSuggestion> ParseSuggestions(
        string content,
        HashSet<string> allowed,
        TagSource source,
        string provider,
        string model)
    {
        try
        {
            var startIdx = content.IndexOf('[');
            var endIdx = content.LastIndexOf(']');
            if (startIdx < 0 || endIdx < 0 || endIdx <= startIdx)
            {
                return [];
            }

            var arrayJson = content[startIdx..(endIdx + 1)];
            using var doc = JsonDocument.Parse(arrayJson);

            var suggestions = new List<TagSuggestion>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                JsonElement tagElement;
                if (!item.TryGetProperty("tag", out tagElement) && !item.TryGetProperty("topic", out tagElement))
                {
                    continue;
                }

                if (!item.TryGetProperty("confidence", out var confidenceElement))
                {
                    continue;
                }

                var tagName = tagElement.GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(tagName) || !allowed.Contains(tagName))
                {
                    continue;
                }

                if (!TryReadConfidence(confidenceElement, out var confidence))
                {
                    continue;
                }

                suggestions.Add(new TagSuggestion(0, tagName, confidence, source, provider, model));
            }

            return suggestions
                .OrderByDescending(s => s.Confidence)
                .ThenBy(s => s.TagName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static bool TryReadConfidence(JsonElement confidenceElement, out float confidence)
    {
        if (confidenceElement.ValueKind == JsonValueKind.Number)
        {
            confidence = confidenceElement.GetSingle();
            return true;
        }

        if (confidenceElement.ValueKind == JsonValueKind.String
            && float.TryParse(confidenceElement.GetString(), out confidence))
        {
            return true;
        }

        confidence = 0;
        return false;
    }
}
