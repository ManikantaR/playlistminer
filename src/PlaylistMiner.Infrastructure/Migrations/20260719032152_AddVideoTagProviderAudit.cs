using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaylistMiner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoTagProviderAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "provider",
                table: "video_tags",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_model",
                table: "video_tags",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "provider",
                table: "video_tags");

            migrationBuilder.DropColumn(
                name: "provider_model",
                table: "video_tags");
        }
    }
}
