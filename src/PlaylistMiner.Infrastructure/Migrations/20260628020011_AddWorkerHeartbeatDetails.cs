using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaylistMiner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkerHeartbeatDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "active_job_type",
                table: "pipeline_runs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "host_environment",
                table: "pipeline_runs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "worker_instance",
                table: "pipeline_runs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "active_job_type",
                table: "pipeline_runs");

            migrationBuilder.DropColumn(
                name: "host_environment",
                table: "pipeline_runs");

            migrationBuilder.DropColumn(
                name: "worker_instance",
                table: "pipeline_runs");
        }
    }
}
