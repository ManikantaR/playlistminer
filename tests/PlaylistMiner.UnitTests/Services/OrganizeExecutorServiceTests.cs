using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PlaylistMiner.Core.DTOs;
using PlaylistMiner.Core.Exceptions;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;
using PlaylistMiner.Infrastructure.Services;

namespace PlaylistMiner.UnitTests.Services;

[Trait("Category", "Unit")]
public class OrganizeExecutorServiceTests
{
    private static PlaylistMinerDbContext CreateDb()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<PlaylistMinerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlaylistMinerDbContext(options);
    }

    private static IConfiguration CreateConfiguration(int batchSize = 20)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Organize:ExecutionBatchSize"] = batchSize.ToString()
            })
            .Build();

    private static void SeedInbox(PlaylistMinerDbContext db, int playlistId = 5)
    {
        db.Playlists.Add(new Playlist
        {
            Id = playlistId,
            YouTubeId = $"PL-{playlistId}",
            Name = "Incoming",
            IsInbox = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task Test_ExecuteAsync_WhenMoveBudgetBlocked_DefersWithoutMovingVideos()
    {
        using var db = CreateDb();
        SeedInbox(db);
        var planner = new Mock<IOrganizePlannerService>();
        planner.Setup(x => x.BuildPlanAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrganizePlanDto(
                2,
                2,
                200,
                [
                    new OrganizePlanItemDto("move", 1, "vid-1", "Video 1", "Incoming", "AI", 11, "AI", 0.91f, 100, "Ready"),
                    new OrganizePlanItemDto("move", 2, "vid-2", "Video 2", "Incoming", "ML", 12, "ML", 0.88f, 100, "Ready")
                ]));

        var organizer = new Mock<IPlaylistOrganizer>(MockBehavior.Strict);
        var operations = new Mock<IOperationsObservabilityService>();
        operations.Setup(x => x.GetMoveBudgetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationsQuotaDto(80, 80, DateTime.UtcNow.AddHours(1), 0, true, "Daily move budget exhausted."));

        var service = new OrganizeExecutorService(
            db,
            planner.Object,
            organizer.Object,
            operations.Object,
            new PipelineRunTracker(db),
            CreateConfiguration(),
            NullLogger<OrganizeExecutorService>.Instance);

        var result = await service.ExecuteAsync();

        result.MovesExecuted.Should().Be(0);
        result.DeferredCount.Should().Be(2);
        result.Errors.Should().ContainSingle("Daily move budget exhausted.");
        organizer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Test_ExecuteAsync_CreatesManagedPlaylistAndExecutesConfiguredBatch()
    {
        using var db = CreateDb();
        SeedInbox(db, playlistId: 7);
        var planner = new Mock<IOrganizePlannerService>();
        planner.Setup(x => x.BuildPlanAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrganizePlanDto(
                3,
                4,
                350,
                [
                    new OrganizePlanItemDto("create_playlist", null, null, null, null, "AI", null, "AI", null, 50, "Missing"),
                    new OrganizePlanItemDto("move", 1, "vid-1", "Video 1", "Incoming", "AI", null, "AI", 0.91f, 100, "Ready"),
                    new OrganizePlanItemDto("move", 2, "vid-2", "Video 2", "Incoming", "ML", 22, "ML", 0.88f, 100, "Ready"),
                    new OrganizePlanItemDto("review", 3, "vid-3", "Video 3", "Incoming", null, null, null, 0.3f, 0, "Review")
                ]));

        var organizer = new Mock<IPlaylistOrganizer>();
        organizer.Setup(x => x.EnsureManagedPlaylistAsync("AI", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaylistMiner.Core.Models.Playlist { Id = 21, Name = "AI", YouTubeId = "PLAI" });
        organizer.Setup(x => x.MoveVideoAsync(1, 7, 21, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        organizer.Setup(x => x.MoveVideoAsync(2, 7, 22, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var operations = new Mock<IOperationsObservabilityService>();
        operations.Setup(x => x.GetMoveBudgetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationsQuotaDto(0, 80, DateTime.UtcNow.AddHours(1), 80, false, "Move budget available."));

        var service = new OrganizeExecutorService(
            db,
            planner.Object,
            organizer.Object,
            operations.Object,
            new PipelineRunTracker(db),
            CreateConfiguration(batchSize: 2),
            NullLogger<OrganizeExecutorService>.Instance);

        var result = await service.ExecuteAsync();

        result.VideosExamined.Should().Be(3);
        result.MovesPlanned.Should().Be(2);
        result.MovesExecuted.Should().Be(2);
        result.DeferredCount.Should().Be(0);
        organizer.Verify(x => x.EnsureManagedPlaylistAsync("AI", It.IsAny<CancellationToken>()), Times.Once);
        organizer.Verify(x => x.MoveVideoAsync(1, 7, 21, It.IsAny<CancellationToken>()), Times.Once);
        organizer.Verify(x => x.MoveVideoAsync(2, 7, 22, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Test_ExecuteAsync_WhenQuotaExhaustsDuringMove_DefersRemainingWork()
    {
        using var db = CreateDb();
        SeedInbox(db, playlistId: 9);
        var planner = new Mock<IOrganizePlannerService>();
        planner.Setup(x => x.BuildPlanAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrganizePlanDto(
                2,
                2,
                200,
                [
                    new OrganizePlanItemDto("move", 1, "vid-1", "Video 1", "Incoming", "AI", 11, "AI", 0.91f, 100, "Ready"),
                    new OrganizePlanItemDto("move", 2, "vid-2", "Video 2", "Incoming", "ML", 12, "ML", 0.88f, 100, "Ready")
                ]));

        var organizer = new Mock<IPlaylistOrganizer>();
        organizer.Setup(x => x.MoveVideoAsync(1, 9, 11, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new QuotaExhaustedException());

        var operations = new Mock<IOperationsObservabilityService>();
        operations.Setup(x => x.GetMoveBudgetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationsQuotaDto(0, 80, DateTime.UtcNow.AddHours(1), 80, false, "Move budget available."));

        var service = new OrganizeExecutorService(
            db,
            planner.Object,
            organizer.Object,
            operations.Object,
            new PipelineRunTracker(db),
            CreateConfiguration(batchSize: 2),
            NullLogger<OrganizeExecutorService>.Instance);

        var result = await service.ExecuteAsync();

        result.MovesExecuted.Should().Be(0);
        result.DeferredCount.Should().Be(2);
        result.Errors.Should().ContainSingle("YouTube API quota exhausted during organize execution. Deferred remaining moves.");

        var run = db.PipelineRuns.Single(r => r.RunId == result.RunId);
        run.Status.Should().Be("deferred");
    }
}
