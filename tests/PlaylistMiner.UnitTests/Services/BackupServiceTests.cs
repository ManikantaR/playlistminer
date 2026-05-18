using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Core.Models;
using PlaylistMiner.Infrastructure.Data;
using PlaylistMiner.Infrastructure.Services;

namespace PlaylistMiner.UnitTests.Services;

[Trait("Category", "Unit")]
public class BackupServiceTests
{
    private static PlaylistMinerDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<PlaylistMinerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PlaylistMinerDbContext(options);
    }

    private static IConfiguration CreateConfig(string? backupDir = null)
    {
        var dict = new Dictionary<string, string?>();
        if (backupDir is not null)
            dict["Backup:Directory"] = backupDir;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public async Task Test_TriggerBackup_CreatesBackupLog()
    {
        // Arrange
        using var db = CreateDb();
        var processMock = new Mock<IProcessRunner>();
        processMock.Setup(p => p.RunAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var config = CreateConfig(Path.GetTempPath());
        var service = new BackupService(db, config, processMock.Object, NullLogger<BackupService>.Instance);

        // Act
        var result = await service.TriggerBackupAsync();

        // Assert
        result.Success.Should().BeTrue();
        var log = await db.BackupLogs.FirstOrDefaultAsync();
        log.Should().NotBeNull();
        log!.Status.Should().Be("success");
        log.Filename.Should().StartWith("playlistminer_");
    }

    [Fact]
    public async Task Test_ListBackups_ReturnsAvailableFiles()
    {
        // Arrange
        using var db = CreateDb();
        db.BackupLogs.AddRange(
            new BackupLog { Filename = "backup_a.sql", SizeBytes = 100, CreatedAt = DateTime.UtcNow.AddDays(-1), Status = "success" },
            new BackupLog { Filename = "backup_b.sql", SizeBytes = 200, CreatedAt = DateTime.UtcNow, Status = "success" }
        );
        await db.SaveChangesAsync();

        var processMock = new Mock<IProcessRunner>();
        var config = CreateConfig();
        var service = new BackupService(db, config, processMock.Object, NullLogger<BackupService>.Instance);

        // Act
        var result = await service.ListBackupsAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Filename.Should().Be("backup_b.sql"); // ordered desc by CreatedAt
    }

    [Fact]
    public async Task Test_CleanupOldBackups_DeletesOlderThan7Days()
    {
        // Arrange
        using var db = CreateDb();
        var oldLog = new BackupLog
        {
            Filename = "old_backup.sql",
            SizeBytes = 100,
            CreatedAt = DateTime.UtcNow.AddDays(-8),
            Status = "success"
        };
        db.BackupLogs.Add(oldLog);
        await db.SaveChangesAsync();

        var processMock = new Mock<IProcessRunner>();
        var config = CreateConfig(Path.GetTempPath());
        var service = new BackupService(db, config, processMock.Object, NullLogger<BackupService>.Instance);

        // Act
        await service.CleanupOldBackupsAsync(keepDays: 7);

        // Assert
        var remaining = await db.BackupLogs.CountAsync();
        remaining.Should().Be(0);
    }

    [Fact]
    public async Task Test_CleanupOldBackups_KeepsRecent()
    {
        // Arrange
        using var db = CreateDb();
        var recentLog = new BackupLog
        {
            Filename = "recent_backup.sql",
            SizeBytes = 100,
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            Status = "success"
        };
        db.BackupLogs.Add(recentLog);
        await db.SaveChangesAsync();

        var processMock = new Mock<IProcessRunner>();
        var config = CreateConfig(Path.GetTempPath());
        var service = new BackupService(db, config, processMock.Object, NullLogger<BackupService>.Instance);

        // Act
        await service.CleanupOldBackupsAsync(keepDays: 7);

        // Assert
        var remaining = await db.BackupLogs.CountAsync();
        remaining.Should().Be(1);
    }
}
