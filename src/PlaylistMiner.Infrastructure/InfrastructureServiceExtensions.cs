using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PlaylistMiner.Core.Categorization;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Infrastructure.Categorization;
using PlaylistMiner.Infrastructure.Repositories;
using PlaylistMiner.Infrastructure.Services;
using PlaylistMiner.Infrastructure.YouTube;

namespace PlaylistMiner.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddYouTubeIntegration(configuration);
        services.AddCategorizationEngine(configuration);
        services.AddRepositories();
        services.AddApplicationServices();
        return services;
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IVideoRepository, VideoRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IPlaylistRepository, PlaylistRepository>();
        services.AddScoped<IUndoRepository, UndoRepository>();
        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IVideoService, VideoService>();
        services.AddScoped<IPlaylistOrganizer, PlaylistOrganizer>();
        services.AddScoped<IOrganizePlannerService, OrganizePlannerService>();
        services.AddScoped<IOrganizeExecutorService, OrganizeExecutorService>();
        services.AddScoped<IOperationsObservabilityService, OperationsObservabilityService>();
        services.AddScoped<IRemoteDuplicateCleanupService, RemoteDuplicateCleanupService>();
        services.AddScoped<IImportService, ImportService>();
        services.AddScoped<ISyncTrigger, SyncTriggerService>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<IProcessRunner, ProcessRunner>();
        services.AddScoped<IPipelineRunTracker, PipelineRunTracker>();
        return services;
    }

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
        services.AddScoped<IQuotaTracker, QuotaTracker>();

        services.AddScoped<IYouTubeApiClient>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("YouTube");
            var tokenProvider = sp.GetRequiredService<ITokenProvider>();
            var quotaTracker = sp.GetRequiredService<IQuotaTracker>();
            return new YouTubeApiClient(httpClient, tokenProvider, quotaTracker);
        });

        // Process-wide single-flight gate so concurrent syncs never write the same tables.
        services.AddSingleton<SyncConcurrencyGate>();
        services.AddScoped<ISyncService, SyncService>();

        return services;
    }

    public static IServiceCollection AddCategorizationEngine(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.Configure<CategorizationOptions>(
            configuration.GetSection(CategorizationOptions.SectionName));

        services.AddScoped<ITagRuleRepository, TagRuleRepository>();
        services.AddScoped<IKeywordMatcher, KeywordMatcher>();
        services.AddScoped<ITfIdfScorer, TfIdfScorer>();
        services.AddScoped<ICategorizationPipeline, CategorizationPipeline>();
        services.AddScoped<ISelfLearningService, SelfLearningService>();

        services.AddHttpClient<IOllamaCategorizer, OllamaCategorizer>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<CategorizationOptions>>().Value;
            client.BaseAddress = new Uri(opts.OllamaBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(35);
        });

        return services;
    }

    /// <summary>
    /// Registers backup, sync-trigger, and process-runner services (all repos + categorization
    /// are expected to be registered via <see cref="AddInfrastructure"/> or separately).
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCategorizationEngine(configuration);
        services.AddScoped<IPlaylistOrganizer, PlaylistOrganizer>();
        services.AddScoped<IOrganizePlannerService, OrganizePlannerService>();
        services.AddScoped<IOrganizeExecutorService, OrganizeExecutorService>();
        services.AddScoped<IOperationsObservabilityService, OperationsObservabilityService>();
        services.AddScoped<ISyncTrigger, SyncTriggerService>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<IProcessRunner, ProcessRunner>();
        services.AddScoped<IPipelineRunTracker, PipelineRunTracker>();
        return services;
    }
}
