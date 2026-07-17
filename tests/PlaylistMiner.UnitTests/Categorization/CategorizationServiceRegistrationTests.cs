using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Infrastructure;

namespace PlaylistMiner.UnitTests.Categorization;

[Trait("Category", "Unit")]
public class CategorizationServiceRegistrationTests
{
    [Fact]
    public async Task Test_AddCategorizationEngine_ConfiguresOllamaTypedClientBaseAddress()
    {
        // Arrange
        Uri? requestedUri = null;
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Categorization:OllamaBaseUrl"] = "http://ollama.test:11434",
                ["Categorization:OllamaModel"] = "qwen2.5:7b-instruct"
            })
            .Build();

        services.AddCategorizationEngine(configuration);
        services.ConfigureAll<HttpClientFactoryOptions>(options =>
        {
            options.HttpMessageHandlerBuilderActions.Add(builder =>
            {
                builder.PrimaryHandler = new RecordingHandler(requestUri =>
                {
                    requestedUri = requestUri;
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""{"models":[]}""")
                    };
                });
            });
        });

        await using var provider = services.BuildServiceProvider();
        var categorizer = provider.GetRequiredService<IOllamaCategorizer>();

        // Act
        var available = await categorizer.IsAvailableAsync();

        // Assert
        available.Should().BeTrue();
        requestedUri.Should().Be(new Uri("http://ollama.test:11434/api/tags"));
    }

    private sealed class RecordingHandler(Func<Uri?, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request.RequestUri));
    }
}
