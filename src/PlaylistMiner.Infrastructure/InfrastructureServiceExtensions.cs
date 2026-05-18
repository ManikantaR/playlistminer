using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Infrastructure.Services;
using PlaylistMiner.Infrastructure.YouTube;

namespace PlaylistMiner.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddYouTubeIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<YouTubeSettings>(configuration.GetSection(YouTubeSettings.SectionName));

        services.AddHttpClient("YouTube", client =>
        {
            client.BaseAddress = new Uri("https://www.googleapis.com/youtube/v3/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        services.AddHttpClient("GoogleOAuth");

        services.AddScoped<ITokenProvider, OAuthTokenProvider>();

        services.AddScoped<IYouTubeApiClient>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("YouTube");
            var tokenProvider = sp.GetRequiredService<ITokenProvider>();
            return new YouTubeApiClient(httpClient, tokenProvider);
        });

        services.AddScoped<ISyncService, SyncService>();

        return services;
    }
}
