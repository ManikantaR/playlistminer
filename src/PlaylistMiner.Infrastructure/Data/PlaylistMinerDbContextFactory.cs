using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PlaylistMiner.Infrastructure.Data;

/// <summary>
/// Used by `dotnet ef` tooling at design-time when Aspire is not running.
/// Set the PLAYLISTMINER_DB connection string or fall back to the dev default.
/// </summary>
public class PlaylistMinerDbContextFactory : IDesignTimeDbContextFactory<PlaylistMinerDbContext>
{
    public PlaylistMinerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("PLAYLISTMINER_DB")
            ?? "Host=localhost;Port=5432;Database=playlistminer;Username=playlistminer;Password=playlistminer";

        var optionsBuilder = new DbContextOptionsBuilder<PlaylistMinerDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsAssembly(typeof(PlaylistMinerDbContext).Assembly.FullName));

        return new PlaylistMinerDbContext(optionsBuilder.Options);
    }
}
