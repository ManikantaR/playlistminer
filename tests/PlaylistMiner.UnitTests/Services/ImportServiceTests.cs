using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PlaylistMiner.Infrastructure.Data;
using PlaylistMiner.Infrastructure.Services;

namespace PlaylistMiner.UnitTests.Services;

[Trait("Category", "Unit")]
public class ImportServiceTests
{
    private static PlaylistMinerDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<PlaylistMinerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlaylistMinerDbContext(opts);
    }

    [Fact]
    public async Task Test_ImportTakeout_ParsesCsvAndCreatesVideos()
    {
        // Arrange
        using var db = CreateDb();
        var svc = new ImportService(db, NullLogger<ImportService>.Instance);

        var csv = """
            Video ID,Playlist name,Created at
            dQw4w9WgXcQ,My Playlist,2024-01-01
            xvFZjo5PgG0,My Playlist,2024-01-02
            """;
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = await svc.ImportTakeoutAsync(stream);

        // Assert
        result.VideosFound.Should().Be(2);
        result.VideosImported.Should().Be(2);
        result.Errors.Should().Be(0);
        result.BatchId.Should().NotBeNullOrEmpty();

        var videos = await db.Videos.ToListAsync();
        videos.Should().HaveCount(2);
        videos.Select(v => v.YouTubeId).Should().Contain("dQw4w9WgXcQ").And.Contain("xvFZjo5PgG0");
    }

    [Fact]
    public async Task Test_ImportTakeout_InvalidCsv_ReturnsErrors()
    {
        // Arrange
        using var db = CreateDb();
        var svc = new ImportService(db, NullLogger<ImportService>.Instance);

        var csv = "NotAVideoId,bad header\n";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = await svc.ImportTakeoutAsync(stream);

        // Assert
        result.Errors.Should().BeGreaterThan(0);
    }
}
