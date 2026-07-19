using Microsoft.EntityFrameworkCore;
using PlaylistMiner.Core.Models;

namespace PlaylistMiner.Infrastructure.Data;

public class PlaylistMinerDbContext(DbContextOptions<PlaylistMinerDbContext> options) : DbContext(options)
{
    public DbSet<Video> Videos => Set<Video>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<VideoTag> VideoTags => Set<VideoTag>();
    public DbSet<Playlist> Playlists => Set<Playlist>();
    public DbSet<PlaylistVideo> PlaylistVideos => Set<PlaylistVideo>();
    public DbSet<TagRule> TagRules => Set<TagRule>();
    public DbSet<UndoLog> UndoLogs => Set<UndoLog>();
    public DbSet<SyncLog> SyncLogs => Set<SyncLog>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<SyncRequest> SyncRequests => Set<SyncRequest>();
    public DbSet<BackupLog> BackupLogs => Set<BackupLog>();
    public DbSet<PipelineRun> PipelineRuns => Set<PipelineRun>();
    public DbSet<PipelineEvent> PipelineEvents => Set<PipelineEvent>();
    public DbSet<OperationRequest> OperationRequests => Set<OperationRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureVideo(modelBuilder);
        ConfigureTag(modelBuilder);
        ConfigureVideoTag(modelBuilder);
        ConfigurePlaylist(modelBuilder);
        ConfigurePlaylistVideo(modelBuilder);
        ConfigureTagRule(modelBuilder);
        ConfigureUndoLog(modelBuilder);
        ConfigureSyncLog(modelBuilder);
        ConfigureImportBatch(modelBuilder);
        ConfigureSetting(modelBuilder);
        ConfigureSyncRequest(modelBuilder);
        ConfigureBackupLog(modelBuilder);
        ConfigurePipelineRun(modelBuilder);
        ConfigurePipelineEvent(modelBuilder);
        ConfigureOperationRequest(modelBuilder);

        SeedData.Apply(modelBuilder);
    }

    private static void ConfigureVideo(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Video>(entity =>
        {
            entity.ToTable("videos");
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(v => v.YouTubeId).HasColumnName("youtube_id").HasMaxLength(11).IsRequired();
            entity.Property(v => v.Title).HasColumnName("title").HasMaxLength(500).IsRequired();
            entity.Property(v => v.Description).HasColumnName("description").HasMaxLength(5000).IsRequired();
            entity.Property(v => v.ChannelName).HasColumnName("channel_name").HasMaxLength(200).IsRequired();
            entity.Property(v => v.ChannelId).HasColumnName("channel_id").HasMaxLength(50).IsRequired();
            entity.Property(v => v.ThumbnailUrl).HasColumnName("thumbnail_url").HasMaxLength(500).IsRequired();
            entity.Property(v => v.Duration).HasColumnName("duration").IsRequired();
            entity.Property(v => v.PublishedAt).HasColumnName("published_at").IsRequired();
            entity.Property(v => v.Status).HasColumnName("status").HasConversion<string>().IsRequired();
            entity.Property(v => v.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(v => v.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.Property(v => v.SyncedAt).HasColumnName("synced_at").IsRequired();

            entity.HasIndex(v => v.YouTubeId).IsUnique().HasDatabaseName("ix_videos_youtube_id");

            // Trigram GIN and full-text GiST indexes are created via raw SQL in the migration
            // to properly use pg_trgm extension and tsvector expression.
        });
    }

    private static void ConfigureTag(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tag>(entity =>
        {
            entity.ToTable("tags");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(t => t.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(t => t.Slug).HasColumnName("slug").HasMaxLength(100).IsRequired();
            entity.Property(t => t.Category).HasColumnName("category").HasMaxLength(100);
            entity.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();

            entity.HasIndex(t => t.Name).IsUnique().HasDatabaseName("ix_tags_name");
            entity.HasIndex(t => t.Slug).IsUnique().HasDatabaseName("ix_tags_slug");
        });
    }

    private static void ConfigureVideoTag(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VideoTag>(entity =>
        {
            entity.ToTable("video_tags");
            entity.HasKey(vt => new { vt.VideoId, vt.TagId, vt.Source });
            entity.Property(vt => vt.VideoId).HasColumnName("video_id");
            entity.Property(vt => vt.TagId).HasColumnName("tag_id");
            entity.Property(vt => vt.Source).HasColumnName("source").HasConversion<string>().IsRequired();
            entity.Property(vt => vt.Confidence).HasColumnName("confidence");
            entity.Property(vt => vt.Provider).HasColumnName("provider").HasMaxLength(50);
            entity.Property(vt => vt.ProviderModel).HasColumnName("provider_model").HasMaxLength(100);
            entity.Property(vt => vt.CreatedAt).HasColumnName("created_at").IsRequired();

            entity.HasOne(vt => vt.Video)
                .WithMany(v => v.VideoTags)
                .HasForeignKey(vt => vt.VideoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(vt => vt.Tag)
                .WithMany(t => t.VideoTags)
                .HasForeignKey(vt => vt.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurePlaylist(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Playlist>(entity =>
        {
            entity.ToTable("playlists");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(p => p.YouTubeId).HasColumnName("youtube_id").HasMaxLength(50).IsRequired();
            entity.Property(p => p.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(p => p.Description).HasColumnName("description").HasMaxLength(1000);
            entity.Property(p => p.IsInbox).HasColumnName("is_inbox").HasDefaultValue(false).IsRequired();
            entity.Property(p => p.IsManaged).HasColumnName("is_managed").HasDefaultValue(false).IsRequired();
            entity.Property(p => p.Topic).HasColumnName("topic").HasMaxLength(200);
            entity.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(p => p.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.Property(p => p.SyncedAt).HasColumnName("synced_at").IsRequired();

            entity.HasIndex(p => p.YouTubeId).IsUnique().HasDatabaseName("ix_playlists_youtube_id");
        });
    }

    private static void ConfigurePlaylistVideo(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlaylistVideo>(entity =>
        {
            entity.ToTable("playlist_videos");
            entity.HasKey(pv => new { pv.PlaylistId, pv.VideoId });
            entity.Property(pv => pv.PlaylistId).HasColumnName("playlist_id");
            entity.Property(pv => pv.VideoId).HasColumnName("video_id");
            entity.Property(pv => pv.Position).HasColumnName("position").IsRequired();
            entity.Property(pv => pv.PlaylistItemId).HasColumnName("playlist_item_id").HasMaxLength(100);
            entity.Property(pv => pv.AddedAt).HasColumnName("added_at").IsRequired();

            entity.HasIndex(pv => pv.VideoId)
                .IsUnique()
                .HasDatabaseName("IX_playlist_videos_video_id");

            entity.HasOne(pv => pv.Playlist)
                .WithMany(p => p.PlaylistVideos)
                .HasForeignKey(pv => pv.PlaylistId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pv => pv.Video)
                .WithMany(v => v.PlaylistVideos)
                .HasForeignKey(pv => pv.VideoId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureTagRule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TagRule>(entity =>
        {
            entity.ToTable("tag_rules");
            entity.HasKey(tr => tr.Id);
            entity.Property(tr => tr.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(tr => tr.TagId).HasColumnName("tag_id");
            entity.Property(tr => tr.Keyword).HasColumnName("keyword").HasMaxLength(200).IsRequired();
            entity.Property(tr => tr.Field).HasColumnName("field").HasConversion<string>().IsRequired();
            entity.Property(tr => tr.Weight).HasColumnName("weight").HasDefaultValue(0.5f).IsRequired();
            entity.Property(tr => tr.IsLearned).HasColumnName("is_learned").HasDefaultValue(false).IsRequired();
            entity.Property(tr => tr.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(tr => tr.UpdatedAt).HasColumnName("updated_at").IsRequired();

            entity.HasOne(tr => tr.Tag)
                .WithMany(t => t.TagRules)
                .HasForeignKey(tr => tr.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureUndoLog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UndoLog>(entity =>
        {
            entity.ToTable("undo_logs");
            entity.HasKey(ul => ul.Id);
            entity.Property(ul => ul.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(ul => ul.VideoId).HasColumnName("video_id");
            entity.Property(ul => ul.Action).HasColumnName("action").HasMaxLength(50).IsRequired();
            entity.Property(ul => ul.SourcePlaylistId).HasColumnName("source_playlist_id");
            entity.Property(ul => ul.TargetPlaylistId).HasColumnName("target_playlist_id");
            entity.Property(ul => ul.PlaylistItemId).HasColumnName("playlist_item_id").HasMaxLength(100);
            entity.Property(ul => ul.PerformedAt).HasColumnName("performed_at").IsRequired();
            entity.Property(ul => ul.ExpiresAt).HasColumnName("expires_at").IsRequired();
            entity.Property(ul => ul.Undone).HasColumnName("undone").HasDefaultValue(false).IsRequired();

            entity.HasOne(ul => ul.Video)
                .WithMany(v => v.UndoLogs)
                .HasForeignKey(ul => ul.VideoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ul => ul.SourcePlaylist)
                .WithMany(p => p.SourceUndoLogs)
                .HasForeignKey(ul => ul.SourcePlaylistId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(ul => ul.TargetPlaylist)
                .WithMany(p => p.TargetUndoLogs)
                .HasForeignKey(ul => ul.TargetPlaylistId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureSyncLog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SyncLog>(entity =>
        {
            entity.ToTable("sync_logs");
            entity.HasKey(sl => sl.Id);
            entity.Property(sl => sl.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(sl => sl.SyncType).HasColumnName("sync_type").HasMaxLength(50).IsRequired();
            entity.Property(sl => sl.StartedAt).HasColumnName("started_at").IsRequired();
            entity.Property(sl => sl.CompletedAt).HasColumnName("completed_at");
            entity.Property(sl => sl.VideosProcessed).HasColumnName("videos_processed").IsRequired();
            entity.Property(sl => sl.VideosCategorized).HasColumnName("videos_categorized").IsRequired();
            entity.Property(sl => sl.Errors).HasColumnName("errors");
            entity.Property(sl => sl.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
        });
    }

    private static void ConfigureImportBatch(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ImportBatch>(entity =>
        {
            entity.ToTable("import_batches");
            entity.HasKey(ib => ib.Id);
            entity.Property(ib => ib.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(ib => ib.Source).HasColumnName("source").HasMaxLength(50).IsRequired();
            entity.Property(ib => ib.Filename).HasColumnName("filename").HasMaxLength(500).IsRequired();
            entity.Property(ib => ib.TotalVideos).HasColumnName("total_videos").IsRequired();
            entity.Property(ib => ib.ImportedCount).HasColumnName("imported_count").IsRequired();
            entity.Property(ib => ib.FailedCount).HasColumnName("failed_count").IsRequired();
            entity.Property(ib => ib.ImportedAt).HasColumnName("imported_at").IsRequired();
        });
    }

    private static void ConfigureSetting(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Setting>(entity =>
        {
            entity.ToTable("settings");
            entity.HasKey(s => s.Key);
            entity.Property(s => s.Key).HasColumnName("key").HasMaxLength(200).IsRequired();
            entity.Property(s => s.Value).HasColumnName("value").IsRequired();
            entity.Property(s => s.UpdatedAt).HasColumnName("updated_at").IsRequired();
        });
    }

    private static void ConfigureSyncRequest(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SyncRequest>(entity =>
        {
            entity.ToTable("sync_requests");
            entity.HasKey(sr => sr.Id);
            entity.Property(sr => sr.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(sr => sr.Type).HasColumnName("type").HasMaxLength(20).IsRequired();
            entity.Property(sr => sr.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            entity.Property(sr => sr.RequestedAt).HasColumnName("requested_at").IsRequired();
            entity.Property(sr => sr.StartedAt).HasColumnName("started_at");
            entity.Property(sr => sr.CompletedAt).HasColumnName("completed_at");
            entity.Property(sr => sr.Error).HasColumnName("error");
        });
    }

    private static void ConfigureBackupLog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BackupLog>(entity =>
        {
            entity.ToTable("backup_logs");
            entity.HasKey(bl => bl.Id);
            entity.Property(bl => bl.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(bl => bl.Filename).HasColumnName("filename").HasMaxLength(500).IsRequired();
            entity.Property(bl => bl.SizeBytes).HasColumnName("size_bytes").IsRequired();
            entity.Property(bl => bl.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(bl => bl.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            entity.Property(bl => bl.Error).HasColumnName("error");
        });
    }

    private static void ConfigurePipelineRun(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PipelineRun>(entity =>
        {
            entity.ToTable("pipeline_runs");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(r => r.RunId).HasColumnName("run_id").HasMaxLength(100).IsRequired();
            entity.Property(r => r.PipelineType).HasColumnName("pipeline_type").HasMaxLength(50).IsRequired();
            entity.Property(r => r.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
            entity.Property(r => r.Phase).HasColumnName("phase").HasMaxLength(100).IsRequired();
            entity.Property(r => r.StartedAt).HasColumnName("started_at").IsRequired();
            entity.Property(r => r.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.Property(r => r.CompletedAt).HasColumnName("completed_at");
            entity.Property(r => r.CurrentMessage).HasColumnName("current_message").HasMaxLength(1000);
            entity.Property(r => r.Error).HasColumnName("error");
            entity.Property(r => r.WorkerInstance).HasColumnName("worker_instance").HasMaxLength(100);
            entity.Property(r => r.HostEnvironment).HasColumnName("host_environment").HasMaxLength(100);
            entity.Property(r => r.ActiveJobType).HasColumnName("active_job_type").HasMaxLength(100);

            // Sync counters
            entity.Property(r => r.PlaylistsDiscovered).HasColumnName("playlists_discovered").HasDefaultValue(0).IsRequired();
            entity.Property(r => r.PlaylistsProcessed).HasColumnName("playlists_processed").HasDefaultValue(0).IsRequired();
            entity.Property(r => r.PlaylistItemsFetched).HasColumnName("playlist_items_fetched").HasDefaultValue(0).IsRequired();
            entity.Property(r => r.UniqueVideoIdsIdentified).HasColumnName("unique_video_ids_identified").HasDefaultValue(0).IsRequired();
            entity.Property(r => r.VideoMetadataBatchesTotal).HasColumnName("video_metadata_batches_total").HasDefaultValue(0).IsRequired();
            entity.Property(r => r.VideoMetadataBatchesCompleted).HasColumnName("video_metadata_batches_completed").HasDefaultValue(0).IsRequired();
            entity.Property(r => r.VideosUpserted).HasColumnName("videos_upserted").HasDefaultValue(0).IsRequired();
            entity.Property(r => r.PlaylistVideoLinksWritten).HasColumnName("playlist_video_links_written").HasDefaultValue(0).IsRequired();
            entity.Property(r => r.VideosArchived).HasColumnName("videos_archived").HasDefaultValue(0).IsRequired();
            entity.Property(r => r.VideosDeferred).HasColumnName("videos_deferred").HasDefaultValue(0).IsRequired();
            entity.Property(r => r.ErrorsCount).HasColumnName("errors_count").HasDefaultValue(0).IsRequired();

            // Categorization counters
            entity.Property(r => r.VideosPendingTagging).HasColumnName("videos_pending_tagging").HasDefaultValue(0).IsRequired();
            entity.Property(r => r.VideosProcessed).HasColumnName("videos_processed").HasDefaultValue(0).IsRequired();
            entity.Property(r => r.VideosTagged).HasColumnName("videos_tagged").HasDefaultValue(0).IsRequired();
            entity.Property(r => r.VideosSkipped).HasColumnName("videos_skipped").HasDefaultValue(0).IsRequired();
            entity.Property(r => r.RuleBasedHits).HasColumnName("rule_based_hits").HasDefaultValue(0).IsRequired();
            entity.Property(r => r.TfidfHits).HasColumnName("tfidf_hits").HasDefaultValue(0).IsRequired();
            entity.Property(r => r.OllamaHits).HasColumnName("ollama_hits").HasDefaultValue(0).IsRequired();

            entity.HasIndex(r => r.RunId).IsUnique().HasDatabaseName("ix_pipeline_runs_run_id");
        });
    }

    private static void ConfigurePipelineEvent(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PipelineEvent>(entity =>
        {
            entity.ToTable("pipeline_events");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(e => e.RunId).HasColumnName("run_id").HasMaxLength(100).IsRequired();
            entity.Property(e => e.OccurredAt).HasColumnName("occurred_at").IsRequired();
            entity.Property(e => e.Level).HasColumnName("level").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Phase).HasColumnName("phase").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Message).HasColumnName("message").HasMaxLength(2000).IsRequired();
            entity.Property(e => e.PayloadJson).HasColumnName("payload_json");

            entity.HasIndex(e => e.RunId).HasDatabaseName("ix_pipeline_events_run_id");
        });
    }

    private static void ConfigureOperationRequest(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OperationRequest>(entity =>
        {
            entity.ToTable("operation_requests");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(o => o.Type).HasColumnName("type").HasMaxLength(50).IsRequired();
            entity.Property(o => o.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            entity.Property(o => o.CreatedBy).HasColumnName("created_by").HasMaxLength(100).IsRequired();
            entity.Property(o => o.Source).HasColumnName("source").HasMaxLength(200);
            entity.Property(o => o.Target).HasColumnName("target").HasMaxLength(200);
            entity.Property(o => o.MaxItems).HasColumnName("max_items");
            entity.Property(o => o.QuotaEstimate).HasColumnName("quota_estimate");
            entity.Property(o => o.NotBefore).HasColumnName("not_before");
            entity.Property(o => o.AllowedWindowStart).HasColumnName("allowed_window_start").HasMaxLength(5);
            entity.Property(o => o.AllowedWindowEnd).HasColumnName("allowed_window_end").HasMaxLength(5);
            entity.Property(o => o.RunId).HasColumnName("run_id").HasMaxLength(100);
            entity.Property(o => o.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(o => o.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.Property(o => o.StartedAt).HasColumnName("started_at");
            entity.Property(o => o.CompletedAt).HasColumnName("completed_at");
            entity.Property(o => o.Error).HasColumnName("error");

            entity.HasIndex(o => o.Status).HasDatabaseName("ix_operation_requests_status");
            entity.HasIndex(o => o.RunId).HasDatabaseName("ix_operation_requests_run_id");
        });
    }
}
