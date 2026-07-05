using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using PlaylistMiner.Core.Categorization;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Categorization;

namespace PlaylistMiner.UnitTests.Categorization;

[Trait("Category", "Unit")]
public class OllamaCategorizerTests
{
    private static IOptions<CategorizationOptions> DefaultOptions()
        => Options.Create(new CategorizationOptions
        {
            OllamaBaseUrl = "http://localhost:11434",
            OllamaModel = "mistral"
        });

    private static (OllamaCategorizer categorizer, Mock<HttpMessageHandler> handler) CreateCategorizer(
        HttpResponseMessage response)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
               .Setup<Task<HttpResponseMessage>>(
                   "SendAsync",
                   ItExpr.IsAny<HttpRequestMessage>(),
                   ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(response);

        var httpClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        var categorizer = new OllamaCategorizer(httpClient, DefaultOptions());
        return (categorizer, handler);
    }

    private static HttpResponseMessage MakeOllamaResponse(string innerJson)
    {
        var responseObj = new { response = innerJson };
        var json = JsonSerializer.Serialize(responseObj);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    [Fact]
    public async Task Test_Categorize_SendsCorrectPrompt()
    {
        // Arrange
        string capturedBody = "";
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
               .Setup<Task<HttpResponseMessage>>(
                   "SendAsync",
                   ItExpr.IsAny<HttpRequestMessage>(),
                   ItExpr.IsAny<CancellationToken>())
               .Returns(async (HttpRequestMessage req, CancellationToken _) =>
               {
                   capturedBody = await req.Content!.ReadAsStringAsync();
                   return MakeOllamaResponse("[]");
               });

        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost:11434") };
        var categorizer = new OllamaCategorizer(httpClient, DefaultOptions());
        var video = new VideoContext("Learn React Hooks", "React functional components tutorial");

        // Act
        await categorizer.CategorizeAsync(video, ["React", "Vue", "Python"]);

        // Assert
        capturedBody.Should().Contain("Learn React Hooks");
        capturedBody.Should().Contain("React functional components tutorial");
    }

    [Fact]
    public async Task Test_Categorize_ParsesResponse_ExtractsTags()
    {
        // Arrange
        var innerJson = """[{"tag":"React","confidence":0.9}]""";
        var (categorizer, _) = CreateCategorizer(MakeOllamaResponse(innerJson));
        var video = new VideoContext("Learn React", "React tutorial");

        // Act
        var result = await categorizer.CategorizeAsync(video, ["React", "Vue"]);

        // Assert
        result.Should().ContainSingle();
        result[0].TagName.Should().Be("React");
        result[0].Confidence.Should().BeApproximately(0.9f, 0.001f);
        result[0].Source.Should().Be(TagSource.Ollama);
    }

    [Fact]
    public async Task Test_Categorize_FiltersSuggestionsOutsideVocabulary_AndOrdersByConfidence()
    {
        // Arrange
        var innerJson = """[{"tag":"Vue","confidence":0.51},{"tag":"React","confidence":0.91},{"tag":"Unknown","confidence":0.99}]""";
        var (categorizer, _) = CreateCategorizer(MakeOllamaResponse(innerJson));
        var video = new VideoContext("Learn React", "React tutorial");

        // Act
        var result = await categorizer.CategorizeAsync(video, ["React", "Vue"]);

        // Assert
        result.Should().HaveCount(2);
        result.Select(x => x.TagName).Should().Equal("React", "Vue");
    }

    [Fact]
    public async Task Test_Categorize_OllamaUnavailable_ReturnsEmpty()
    {
        // Arrange
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
               .Setup<Task<HttpResponseMessage>>(
                   "SendAsync",
                   ItExpr.IsAny<HttpRequestMessage>(),
                   ItExpr.IsAny<CancellationToken>())
               .ThrowsAsync(new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost:11434") };
        var categorizer = new OllamaCategorizer(httpClient, DefaultOptions());
        var video = new VideoContext("Learn React", "React tutorial");

        // Act
        var result = await categorizer.CategorizeAsync(video, ["React"]);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Test_Categorize_InvalidResponse_ReturnsEmpty()
    {
        // Arrange
        var badResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not valid json at all", Encoding.UTF8, "application/json")
        };
        var (categorizer, _) = CreateCategorizer(badResponse);
        var video = new VideoContext("Learn React", "React tutorial");

        // Act
        var result = await categorizer.CategorizeAsync(video, ["React"]);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Test_IsAvailable_ReturnsTrueWhenReachable()
    {
        // Arrange
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
               .Setup<Task<HttpResponseMessage>>(
                   "SendAsync",
                   ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get && r.RequestUri!.PathAndQuery.Contains("api/tags")),
                   ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost:11434") };
        var categorizer = new OllamaCategorizer(httpClient, DefaultOptions());

        // Act
        var result = await categorizer.IsAvailableAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Test_IsAvailable_ReturnsFalseWhenUnreachable()
    {
        // Arrange
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
               .Setup<Task<HttpResponseMessage>>(
                   "SendAsync",
                   ItExpr.IsAny<HttpRequestMessage>(),
                   ItExpr.IsAny<CancellationToken>())
               .ThrowsAsync(new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost:11434") };
        var categorizer = new OllamaCategorizer(httpClient, DefaultOptions());

        // Act
        var result = await categorizer.IsAvailableAsync();

        // Assert
        result.Should().BeFalse();
    }
}
