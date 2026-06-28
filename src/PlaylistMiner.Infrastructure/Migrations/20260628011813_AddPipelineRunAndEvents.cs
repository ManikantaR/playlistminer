using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PlaylistMiner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelineRunAndEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pipeline_events",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    run_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    phase = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pipeline_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pipeline_runs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    run_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    pipeline_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    phase = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    current_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    error = table.Column<string>(type: "text", nullable: true),
                    playlists_discovered = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    playlists_processed = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    playlist_items_fetched = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    unique_video_ids_identified = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    video_metadata_batches_total = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    video_metadata_batches_completed = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    videos_upserted = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    playlist_video_links_written = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    videos_archived = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    videos_deferred = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    errors_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    videos_pending_tagging = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    videos_processed = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    videos_tagged = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    videos_skipped = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    rule_based_hits = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tfidf_hits = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ollama_hits = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pipeline_runs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_events_run_id",
                table: "pipeline_events",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_runs_run_id",
                table: "pipeline_runs",
                column: "run_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pipeline_events");

            migrationBuilder.DropTable(
                name: "pipeline_runs");
        }
    }
}
