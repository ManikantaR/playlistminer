using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaylistMiner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaylistItemIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "playlist_item_id",
                table: "undo_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "playlist_item_id",
                table: "playlist_videos",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "playlist_item_id",
                table: "undo_logs");

            migrationBuilder.DropColumn(
                name: "playlist_item_id",
                table: "playlist_videos");
        }
    }
}
