using PlaylistMiner.Infrastructure;
using PlaylistMiner.Infrastructure.Data;
using PlaylistMiner.Worker;
using PlaylistMiner.Worker.Jobs;
using Quartz;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<PlaylistMinerDbContext>("playlistminer");
builder.Services.AddYouTubeIntegration(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration);

// Quartz scheduler.
// In-memory (RAM) job store: all jobs below are code-defined cron triggers re-registered
// on every startup, and on-demand sync uses the SyncRequest DB table (not Quartz), so a
// persistent store adds schema-provisioning overhead with no real benefit here. If misfire
// durability across restarts is ever needed, switch back to UsePersistentStore + UsePostgres
// AND provision the qrtz_* schema (Quartz tables_postgres.sql, via an EF migration).
builder.Services.AddQuartz(q =>
{
    void AddJob<T>(string defaultCron, string configKey, string jobKey) where T : IJob
    {
        var key = new JobKey(jobKey);
        q.AddJob<T>(opts => opts.WithIdentity(key));
        q.AddTrigger(opts => opts.ForJob(key)
            .WithCronSchedule(builder.Configuration[$"Quartz:{configKey}"] ?? defaultCron));
    }

    AddJob<SyncJob>("0 0 2 * * ?", "SyncCron", "sync-job");
    AddJob<InboxProcessingJob>("0 0 */6 * * ?", "InboxCron", "inbox-job");
    AddJob<CategorizationJob>("0 30 2 * * ?", "CategorizationCron", "categorization-job");
    AddJob<UndoCleanupJob>("0 0 3 * * ?", "CleanupCron", "cleanup-job");
    AddJob<BackupJob>("0 0 4 * * ?", "BackupCron", "backup-job");
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
builder.Services.AddHostedService<SyncTriggerHostedService>();

var host = builder.Build();
host.Run();
