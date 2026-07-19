using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using PlaylistMiner.Core.Categorization;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Categorization;

namespace PlaylistMiner.UnitTests.Categorization;

[Trait("Category", "Unit")]
public class PublicAiCategorizerTests
{
    private static AutomationPolicyDto MakePolicy(
        string provider,
        string model,
        string transcriptCloudPolicy = "never") =>
        new(
            Mode: "aggressive_with_undo",
            HighConfidenceThreshold: 0.9f,
            ReviewThreshold: 0.65f,
            DailyMoveBudget: 80,
            NightlyRestoreBudget: 150,
            CleanupRecommendationCount: 5,
            OffPeakWindowStart: "23:00",
            OffPeakWindowEnd: "05:00",
            PublicAiFallbackEnabled: true,
            PublicAiProvider: provider,
            PublicAiModel: model,
            TranscriptCloudPolicy: transcriptCloudPolicy,
            IsPaused: false);

    [Fact]
    public async Task Test_CategorizeAsync_WhenGeminiConfigured_ParsesResponseAndRecordsProviderMetadata()
    {
        // Arrange
        var responseJson = """
            {
              "candidates": [
                {
                  "content": {
                    "parts": [
                      {
                        "text": "[{\"tag\":\"React\",\"confidence\":0.91},{\"tag\":\"Unknown\",\"confidence\":0.99}]"
                      }
                    ]
                  }
                }
              ]
            }
            """;
        var (categorizer, _) = CreateCategorizer(responseJson, new PublicAiOptions
        {
            GeminiApiKey = "gemini-key"
        });

        // Act
        var result = await categorizer.CategorizeAsync(
            new VideoContext("React agents", "Hooks and components"),
            ["React", "Python"],
            MakePolicy("gemini", "gemini-3.1-flash-lite"));

        // Assert
        result.Should().ContainSingle();
        result[0].TagName.Should().Be("React");
        result[0].Source.Should().Be(TagSource.Gemini);
        result[0].Provider.Should().Be("gemini");
        result[0].ProviderModel.Should().Be("gemini-3.1-flash-lite");
    }

    [Fact]
    public async Task Test_CategorizeAsync_WhenTranscriptCloudPolicyNever_SendsOnlyVideoMetadata()
    {
        // Arrange
        string capturedBody = "";
        var (categorizer, _) = CreateCategorizer(
            """{"candidates":[{"content":{"parts":[{"text":"[]"}]}}]}""",
            new PublicAiOptions { GeminiApiKey = "gemini-key" },
            body => capturedBody = body);

        // Act
        await categorizer.CategorizeAsync(
            new VideoContext("React agents", "Hooks and components"),
            ["React"],
            MakePolicy("gemini", "gemini-3.1-flash-lite", transcriptCloudPolicy: "never"));

        // Assert
        capturedBody.Should().Contain("React agents");
        capturedBody.Should().Contain("Hooks and components");
        capturedBody.Should().NotContain("Transcript:");
    }

    [Fact]
    public async Task Test_CategorizeAsync_WhenOpenAiConfigured_ParsesResponseAndRecordsProviderMetadata()
    {
        // Arrange
        var responseJson = """
            {
              "choices": [
                {
                  "message": {
                    "content": "[{\"tag\":\"Python\",\"confidence\":0.82}]"
                  }
                }
              ]
            }
            """;
        var (categorizer, _) = CreateCategorizer(responseJson, new PublicAiOptions
        {
            OpenAIApiKey = "openai-key"
        });

        // Act
        var result = await categorizer.CategorizeAsync(
            new VideoContext("Python agents", "Async workers"),
            ["React", "Python"],
            MakePolicy("openai", "gpt-5.6-luna"));

        // Assert
        result.Should().ContainSingle();
        result[0].TagName.Should().Be("Python");
        result[0].Source.Should().Be(TagSource.OpenAI);
        result[0].Provider.Should().Be("openai");
        result[0].ProviderModel.Should().Be("gpt-5.6-luna");
    }

    [Fact]
    public async Task Test_CategorizeAsync_WhenProviderKeyMissing_ReturnsEmptyWithoutCallingProvider()
    {
        // Arrange
        var handler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(handler.Object);
        var categorizer = new PublicAiCategorizer(
            httpClient,
            Options.Create(new PublicAiOptions()),
            NullLogger<PublicAiCategorizer>.Instance);

        // Act
        var result = await categorizer.CategorizeAsync(
            new VideoContext("React agents", "Hooks and components"),
            ["React"],
            MakePolicy("gemini", "gemini-3.1-flash-lite"));

        // Assert
        result.Should().BeEmpty();
        handler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    private static (PublicAiCategorizer categorizer, Mock<HttpMessageHandler> handler) CreateCategorizer(
        string responseJson,
        PublicAiOptions options,
        Action<string>? captureBody = null)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken _) =>
            {
                if (captureBody is not null && request.Content is not null)
                {
                    captureBody(await request.Content.ReadAsStringAsync());
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                };
            });

        var httpClient = new HttpClient(handler.Object);
        return (
            new PublicAiCategorizer(httpClient, Options.Create(options), NullLogger<PublicAiCategorizer>.Instance),
            handler);
    }
}
