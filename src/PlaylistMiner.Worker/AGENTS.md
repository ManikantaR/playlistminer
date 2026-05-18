# PlaylistMiner.Worker — Agent Instructions

## Role
You are working on the Quartz.NET Worker Service for PlaylistMiner. This handles background jobs: YouTube sync, categorization, inbox processing, and undo cleanup.

## Rules
1. **TDD First:** Write the failing xUnit test before any implementation code.
2. **Jobs are stateless.** All state persists in PostgreSQL.
3. **Respect YouTube API quota.** Track usage, defer if exhausted (10,000 units/day).
4. **Graceful degradation.** If Ollama is unavailable, skip LLM categorization — never throw.
5. **Always use CancellationToken** for graceful shutdown support.
6. **Log with structured logging** (ILogger<T>) — job name, video count, errors.
7. **Idempotent jobs.** Re-running a job should not create duplicates.

## Project References
- This project → Core + Infrastructure + ServiceDefaults
- Quartz jobs implement `IJob` with `Execute(IJobExecutionContext)`
- YouTube client, sync service, categorization pipeline are in Infrastructure

## Job Schedule (configurable via appsettings)
- SyncJob: daily 2:00 AM
- InboxProcessingJob: every 6 hours
- CategorizationJob: daily 2:30 AM
- UndoCleanupJob: daily 3:00 AM

## Manual Sync Trigger
- API inserts row in `sync_requests` table with status=pending
- Worker polls every 10 seconds for pending requests
- Worker updates status as it processes (running → completed/failed)

## When Adding a Job
1. Write job test with mocked dependencies (Red)
2. Implement job (Green)
3. Register in Quartz config with cron schedule
4. Add integration test if job touches DB
