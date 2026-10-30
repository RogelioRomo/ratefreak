using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RateFreak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAlbums : Migration
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
                sql: "media_type IN ('movie', 'tv_show', 'album')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM media_items WHERE media_type = 'album') THEN
                        RAISE EXCEPTION 'Cannot downgrade AddAlbums: albums exist; migrate or remove album data explicitly before rollback.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_media_items_type",
                table: "media_items");

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_items_type",
                table: "media_items",
                sql: "media_type IN ('movie', 'tv_show')");
        }
    }
}
