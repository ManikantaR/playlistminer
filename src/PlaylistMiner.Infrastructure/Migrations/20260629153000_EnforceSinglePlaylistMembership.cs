using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaylistMiner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSinglePlaylistMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT
                        pv.playlist_id,
                        pv.video_id,
                        ROW_NUMBER() OVER (
                            PARTITION BY pv.video_id
                            ORDER BY
                                CASE WHEN p.is_inbox THEN 1 ELSE 0 END,
                                pv.playlist_id
                        ) AS rn
                    FROM playlist_videos pv
                    INNER JOIN playlists p ON p.id = pv.playlist_id
                )
                DELETE FROM playlist_videos pv
                USING ranked r
                WHERE pv.playlist_id = r.playlist_id
                  AND pv.video_id = r.video_id
                  AND r.rn > 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_playlist_videos_video_id",
                table: "playlist_videos");

            migrationBuilder.CreateIndex(
                name: "IX_playlist_videos_video_id",
                table: "playlist_videos",
                column: "video_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_playlist_videos_video_id",
                table: "playlist_videos");

            migrationBuilder.CreateIndex(
                name: "IX_playlist_videos_video_id",
                table: "playlist_videos",
                column: "video_id");
        }
    }
}
