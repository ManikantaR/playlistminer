using Microsoft.EntityFrameworkCore;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace PlaylistMiner.IntegrationTests.Repositories;

public abstract class RepositoryTestBase : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("playlistminer_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    protected PlaylistMinerDbContext DbContext { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var opts = new DbContextOptionsBuilder<PlaylistMinerDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        DbContext = new PlaylistMinerDbContext(opts);
        await DbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected static Video MakeVideo(string youtubeId = "vid001", string title = "Test Video") => new()
    {
        YouTubeId = youtubeId,
        Title = title,
        Description = "A test video",
        ChannelName = "Test Channel",
        ChannelId = "UC_test",
        ThumbnailUrl = "https://img.youtube.com/vi/test/0.jpg",
        Duration = TimeSpan.FromMinutes(10),
        PublishedAt = DateTime.UtcNow,
        Status = VideoStatus.Active,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        SyncedAt = DateTime.UtcNow
    };

    protected static Playlist MakePlaylist(string youtubeId = "PL001", string name = "Test Playlist") => new()
    {
        YouTubeId = youtubeId,
        Name = name,
        IsInbox = false,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        SyncedAt = DateTime.UtcNow
    };
}
