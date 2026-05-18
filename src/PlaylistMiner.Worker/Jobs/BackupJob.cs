using Microsoft.Extensions.Logging;
using PlaylistMiner.Core.Interfaces;
using Quartz;

namespace PlaylistMiner.Worker.Jobs;

[DisallowConcurrentExecution]
public class BackupJob(IBackupService backupService, ILogger<BackupJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var result = await backupService.TriggerBackupAsync(context.CancellationToken);
            await backupService.CleanupOldBackupsAsync(7, context.CancellationToken);

            if (result.Success)
                logger.LogInformation("Backup created: {Filename}", result.Filename);
            else
                logger.LogError("Backup failed: {Error}", result.Error);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Backup job encountered an unexpected error");
        }
    }
}
