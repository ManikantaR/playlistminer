# Prompt 07: Quartz.NET Scheduler & Background Jobs

## Context
PlaylistMiner Worker runs Quartz.NET jobs for daily sync, categorization of new videos, undo log cleanup, and manual sync triggers. The Worker is a separate container sharing the same DB. TDD with xUnit.

## Prompt 07a: Quartz Job Setup (TDD)

```
TDD — tests first:

SyncJobTests.cs:
- Test_SyncJob_ExecutesFullSync
- Test_SyncJob_LogsSyncResult
- Test_SyncJob_OnError_LogsAndContinues
- Test_SyncJob_RespectsCancellation

InboxProcessingJobTests.cs:
- Test_ProcessInbox_SyncsInboxPlaylist
- Test_ProcessInbox_CategorizeNewVideos
- Test_ProcessInbox_SkipsAlreadyCategorized

UndoCleanupJobTests.cs:
- Test_Cleanup_DeletesExpiredEntries
- Test_Cleanup_KeepsNonExpiredEntries

CategorizationJobTests.cs:
- Test_CategorizeJob_ProcessesUncategorizedVideos
- Test_CategorizeJob_BatchesProcessing
- Test_CategorizeJob_SkipsAlreadySuggested

Then implement in PlaylistMiner.Worker/Jobs/:

SyncJob : IJob
- Runs ISyncService.FullSyncAsync()
- Cron: "0 0 2 * * ?" (daily at 2 AM, configurable)

InboxProcessingJob : IJob
- Syncs only inbox playlist
- Runs categorization pipeline on new inbox videos
- Cron: "0 0 */6 * * ?" (every 6 hours, configurable)

CategorizationJob : IJob
- Finds videos with no suggestions and no manual tags
- Runs categorization pipeline in batches of 50
- Cron: "0 30 2 * * ?" (daily at 2:30 AM, after sync)

UndoCleanupJob : IJob
- Deletes undo_log entries where expires_at < now
- Cron: "0 0 3 * * ?" (daily at 3 AM)

Configure in Worker Program.cs:
- Register all jobs with Quartz
- Load cron schedules from appsettings.json
- Use persistent job store (PostgreSQL via Quartz.Serialization.Json)
```

## Prompt 07b: Manual Sync Trigger

```
The API needs to trigger sync jobs on-demand when user clicks "Sync Now".

Implement a background channel pattern:

In Core:
- ISyncTrigger interface with TriggerAsync(SyncType type) method

In Infrastructure:
- SyncTriggerChannel using System.Threading.Channels
- API writes to channel (fire-and-forget, returns 202)
- Worker reads from channel and executes sync

In Worker:
- SyncTriggerHostedService : BackgroundService
- Reads from channel continuously
- Executes appropriate sync based on type

TDD tests:
- Test_TriggerSync_WritesToChannel
- Test_Worker_ReadsFromChannel_ExecutesSync
- Test_Worker_HandlesMultipleTriggersSequentially

Note: Since API and Worker are separate containers, use PostgreSQL as the message broker:
- Create a sync_requests table (id, type, status, requested_at, started_at, completed_at)
- API inserts a row with status=pending
- Worker polls every 10 seconds for pending requests
- Worker updates status as it processes
- API can query status via GET /api/sync/status
```

## Prompt 07c: Database Backup Job (TDD)

```
TDD — tests first:

BackupJobTests.cs:
- Test_BackupJob_CreatesDumpFile
- Test_BackupJob_UsesTimestampedFilename
- Test_BackupJob_LogsToBackupLog
- Test_BackupJob_CleansUpOldBackups_KeepsLast7
- Test_BackupJob_HandlesFailure_LogsError

BackupServiceTests.cs:
- Test_TriggerBackup_CreatesBackupFile
- Test_ListBackups_ReturnsAvailableFiles
- Test_GetBackupStream_ReturnsFileContents
- Test_CleanupOldBackups_DeletesOlderThan7Days

Then implement:

BackupJob : IJob
- Cron: "0 0 4 * * ?" (daily at 4 AM)
- Execute pg_dump via Npgsql ProcessStartInfo or via DbConnection command
- Output to configurable backup directory (default: ./data/backups/)
- Filename format: playlistminer_YYYYMMDD_HHmmss.sql
- Create backup_log entry with filename, size, status
- After successful backup, clean up files older than 7 days
- On failure: log error, create backup_log with status=failed

IBackupService in Core:
- Task<BackupResult> TriggerBackupAsync(CancellationToken ct)
- Task<List<BackupInfo>> ListBackupsAsync(CancellationToken ct)
- Task<Stream> GetBackupStreamAsync(string filename, CancellationToken ct)

BackupController in Api:
- POST /api/backup/trigger → 202 Accepted
- GET /api/backup/history → list of backups
- GET /api/backup/download/{filename} → file download

backup_log table:
- id, filename, size_bytes, created_at, status, error

Add backup directory to Podman volume mount and .gitignore.
```

## Verification
- Worker container starts and registers all Quartz jobs (including BackupJob)
- Jobs execute on schedule in development
- Manual sync trigger from API creates request and Worker picks it up
- Undo cleanup removes expired entries
- Backup job creates .sql file in ./data/backups/
- Old backups auto-cleaned after 7 days
- Backup download works via API
- All job tests pass
