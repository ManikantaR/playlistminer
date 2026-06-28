using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using PlaylistMiner.Infrastructure.Data;

namespace PlaylistMiner.IntegrationTests.Api;

public class PlaylistMinerWebAppFactory : WebApplicationFactory<Program>
{
    private readonly Action<IServiceCollection>? _configureServices;
    private readonly string _databaseName = "TestDb_" + Guid.NewGuid();

    public PlaylistMinerWebAppFactory(Action<IServiceCollection>? configureServices = null)
    {
        _configureServices = configureServices;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Remove ALL EF/DbContext descriptors related to PlaylistMinerDbContext
            // AddNpgsqlDbContext (Aspire) uses AddDbContextPool, which registers multiple service types:
            // - DbContextOptions<PlaylistMinerDbContext>
            // - DbContextOptions (non-generic)
            // - IDbContextPool<PlaylistMinerDbContext>
            // - PlaylistMinerDbContext itself (via pool factory)
            var contextType = typeof(PlaylistMinerDbContext);
            var descriptorsToRemove = services
                .Where(d =>
                {
                    var st = d.ServiceType;
                    // Direct DbContext registration
                    if (st == contextType) return true;
                    // DbContextOptions (non-generic)
                    if (st == typeof(DbContextOptions)) return true;
                    // Any generic service whose single type argument is PlaylistMinerDbContext
                    if (st.IsGenericType && st.GetGenericArguments().Length == 1
                        && st.GetGenericArguments()[0] == contextType) return true;
                    // Implementation type is PlaylistMinerDbContext
                    if (d.ImplementationType == contextType) return true;
                    return false;
                })
                .ToList();

            foreach (var d in descriptorsToRemove)
                services.Remove(d);

            // Register a fresh InMemory DbContext per-test
            services.AddDbContext<PlaylistMinerDbContext>(opts =>
                opts.UseInMemoryDatabase(_databaseName),
                ServiceLifetime.Scoped);

            _configureServices?.Invoke(services);
        });
    }
}
