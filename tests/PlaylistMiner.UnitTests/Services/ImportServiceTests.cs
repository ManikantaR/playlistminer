using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
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

    private static Mock<IQuotaTracker> CreateQuotaTrackerMock()
    {
        var mock = new Mock<IQuotaTracker>();
        mock.Setup(q => q.IsQuotaExhaustedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        return mock;
    }

    private static Mock<IYouTubeApiClient> CreateYouTubeClientMock()
    {
        var mock = new Mock<IYouTubeApiClient>();
        mock.Setup(c => c.GetVideoMetadataAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> ids, CancellationToken _) =>
                ids.Select(id => new VideoMetadataDto(
                    id, $"Title {id}", "Description", "Channel", "UC123",
                    "https://thumb.jpg", TimeSpan.FromMinutes(5),
                    DateTime.UtcNow, VideoStatus.Active)).ToList());
        return mock;
    }

    [Fact]
    public async Task Test_ImportTakeout_ParsesCsvAndCreatesVideos()
    {
        using var db = CreateDb();
        var ytMock = CreateYouTubeClientMock();
        var svc = new ImportService(db, ytMock.Object, CreateQuotaTrackerMock().Object, NullLogger<ImportService>.Instance);

        var csv = "Video Id,Time Added\ndQw4w9WgXcQ,2024-01-01\nxvFZjo5PgG0,2024-01-02";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await svc.ImportTakeoutAsync(stream);

        result.VideosFound.Should().Be(2);
        result.VideosImported.Should().Be(2);
        result.Errors.Should().Be(0);

        var videos = await db.Videos.ToListAsync();
        videos.Should().HaveCount(2);
        videos.Select(v => v.YouTubeId).Should().Contain("dQw4w9WgXcQ").And.Contain("xvFZjo5PgG0");
    }

    [Fact]
    public async Task Test_ImportTakeout_SkipsDuplicates()
    {
        using var db = CreateDb();
        db.Videos.Add(new Video
        {
            YouTubeId = "existing1",
            Title = "Existing",
            Description = "",
            ChannelName = "",
            ChannelId = "",
            ThumbnailUrl = "",
            Status = VideoStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var ytMock = CreateYouTubeClientMock();
        var svc = new ImportService(db, ytMock.Object, CreateQuotaTrackerMock().Object, NullLogger<ImportService>.Instance);

        var csv = "Video Id,Time Added\nexisting1,2024-01-01\nnewvid1,2024-01-02";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await svc.ImportTakeoutAsync(stream);

        result.VideosFound.Should().Be(2);
        result.VideosImported.Should().Be(1);
        result.Skipped.Should().Be(1);
    }

    [Fact]
    public async Task Test_ImportTakeout_InvalidCsv_ReturnsErrors()
    {
        using var db = CreateDb();
        var ytMock = CreateYouTubeClientMock();
        var svc = new ImportService(db, ytMock.Object, CreateQuotaTrackerMock().Object, NullLogger<ImportService>.Instance);

        var csv = "NotAVideoId,bad header\ndata,more";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await svc.ImportTakeoutAsync(stream);

        result.Errors.Should().BeGreaterThan(0);
        result.VideosImported.Should().Be(0);
    }

    [Fact]
    public async Task Test_ImportTakeout_HydratesViaYouTubeApi()
    {
        using var db = CreateDb();
        var ytMock = CreateYouTubeClientMock();
        var svc = new ImportService(db, ytMock.Object, CreateQuotaTrackerMock().Object, NullLogger<ImportService>.Instance);

        var csv = "Video Id\nvid001\nvid002\nvid003";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        await svc.ImportTakeoutAsync(stream);

        ytMock.Verify(c => c.GetVideoMetadataAsync(
            It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce());

        var videos = await db.Videos.ToListAsync();
        videos.Should().AllSatisfy(v => v.Title.Should().StartWith("Title "));
    }

    [Fact]
    public async Task Test_ImportTakeout_RecordsImportBatch()
    {
        using var db = CreateDb();
        var ytMock = CreateYouTubeClientMock();
        var svc = new ImportService(db, ytMock.Object, CreateQuotaTrackerMock().Object, NullLogger<ImportService>.Instance);

        var csv = "Video Id\nvid001";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        await svc.ImportTakeoutAsync(stream);

        var batches = await db.ImportBatches.ToListAsync();
        batches.Should().HaveCount(1);
        batches[0].Source.Should().Be("Takeout");
        batches[0].ImportedCount.Should().Be(1);
    }

    [Fact]
    public async Task Test_ImportTakeout_UnavailableVideos_MarkedCorrectly()
    {
        using var db = CreateDb();
        var ytMock = new Mock<IYouTubeApiClient>();
        ytMock.Setup(c => c.GetVideoMetadataAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VideoMetadataDto>());

        var svc = new ImportService(db, ytMock.Object, CreateQuotaTrackerMock().Object, NullLogger<ImportService>.Instance);

        var csv = "Video Id\ndeleted_vid";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        await svc.ImportTakeoutAsync(stream);

        var video = await db.Videos.FirstOrDefaultAsync(v => v.YouTubeId == "deleted_vid");
        video.Should().NotBeNull();
        video!.Status.Should().Be(VideoStatus.Unavailable);
        video.Title.Should().Contain("Unavailable");
    }
}
