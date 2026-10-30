using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RateFreak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_media_items_type",
                table: "media_items");

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_items_type",
                table: "media_items",
                sql: "media_type IN ('movie', 'tv_show', 'album', 'video_game')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE media_items IN ACCESS EXCLUSIVE MODE;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM media_items WHERE media_type = 'video_game') THEN
                        RAISE EXCEPTION 'Cannot downgrade AddVideoGames: video games exist; migrate game data explicitly before rollback.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "ck_media_items_type",
                table: "media_items");

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_items_type",
                table: "media_items",
                sql: "media_type IN ('movie', 'tv_show', 'album')");
        }
    }
}
