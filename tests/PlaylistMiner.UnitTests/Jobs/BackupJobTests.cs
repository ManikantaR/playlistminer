using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PlaylistMiner.Core.Interfaces;
using PlaylistMiner.Worker.Jobs;
using Quartz;

namespace PlaylistMiner.UnitTests.Jobs;

[Trait("Category", "Unit")]
public class BackupJobTests
{
    private static IJobExecutionContext CreateContext()
    {
        var mock = new Mock<IJobExecutionContext>();
        mock.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }

    [Fact]
    public async Task Test_BackupJob_TriggersBackupService()
    {
        // Arrange
        var backupMock = new Mock<IBackupService>();
        backupMock.Setup(b => b.TriggerBackupAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BackupResult("file.sql", 1024, true, null));
        backupMock.Setup(b => b.CleanupOldBackupsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var logger = new Mock<ILogger<BackupJob>>();
        var job = new BackupJob(backupMock.Object, logger.Object);

        // Act
        await job.Execute(CreateContext());

        // Assert
        backupMock.Verify(b => b.TriggerBackupAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Test_BackupJob_CleansUpOldBackups_KeepsLast7()
    {
        // Arrange
        var backupMock = new Mock<IBackupService>();
        backupMock.Setup(b => b.TriggerBackupAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BackupResult("file.sql", 1024, true, null));
        backupMock.Setup(b => b.CleanupOldBackupsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var logger = new Mock<ILogger<BackupJob>>();
        var job = new BackupJob(backupMock.Object, logger.Object);

        // Act
        await job.Execute(CreateContext());

        // Assert
        backupMock.Verify(b => b.CleanupOldBackupsAsync(7, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Test_BackupJob_LogsSuccess()
    {
        // Arrange
        var backupMock = new Mock<IBackupService>();
        backupMock.Setup(b => b.TriggerBackupAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BackupResult("backup_20250101.sql", 2048, true, null));
        backupMock.Setup(b => b.CleanupOldBackupsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var loggerMock = new Mock<ILogger<BackupJob>>();
        var job = new BackupJob(backupMock.Object, loggerMock.Object);

        // Act
        await job.Execute(CreateContext());

        // Assert — Info log with filename
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("backup_20250101.sql")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once());
    }

    [Fact]
    public async Task Test_BackupJob_LogsFailure()
    {
        // Arrange
        var backupMock = new Mock<IBackupService>();
        backupMock.Setup(b => b.TriggerBackupAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BackupResult("file.sql", 0, false, "pg_dump not found"));
        backupMock.Setup(b => b.CleanupOldBackupsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var loggerMock = new Mock<ILogger<BackupJob>>();
        var job = new BackupJob(backupMock.Object, loggerMock.Object);

        // Act
        await job.Execute(CreateContext());

        // Assert — Error log with error message
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("pg_dump not found")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once());
    }

    [Fact]
    public async Task Test_BackupJob_HandlesException()
    {
        // Arrange
        var backupMock = new Mock<IBackupService>();
        backupMock.Setup(b => b.TriggerBackupAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("unexpected error"));
        var loggerMock = new Mock<ILogger<BackupJob>>();
        var job = new BackupJob(backupMock.Object, loggerMock.Object);

        // Act — must not throw
        var act = async () => await job.Execute(CreateContext());

        // Assert
        await act.Should().NotThrowAsync();
    }
}
