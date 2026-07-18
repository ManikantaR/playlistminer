using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PlaylistMiner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "operation_requests",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    target = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    max_items = table.Column<int>(type: "integer", nullable: true),
                    quota_estimate = table.Column<int>(type: "integer", nullable: true),
                    not_before = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    allowed_window_start = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    allowed_window_end = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    run_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operation_requests", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_operation_requests_run_id",
                table: "operation_requests",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "ix_operation_requests_status",
                table: "operation_requests",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operation_requests");
        }
    }
}
