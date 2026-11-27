using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RateFreak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSongs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE media_items, catalog_categories, catalog_category_aliases IN ACCESS EXCLUSIVE MODE;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM catalog_categories
                        WHERE id = 'f11e0000-0000-0000-0000-000000000006'
                           OR (scope = 'public' AND normalized_name = 'SONGS')) THEN
                        RAISE EXCEPTION 'Cannot add Songs category: its seed identity or public name is occupied. Review and rename/reconcile the existing category explicitly before retrying; no category is repurposed automatically.';
                    END IF;
                END $$;
                INSERT INTO catalog_categories (id, name, normalized_name, scope, is_hidden, is_seeded, revision, created_at, updated_at)
                VALUES ('f11e0000-0000-0000-0000-000000000006', 'Songs', 'SONGS', 'public', false, true, 1, now(), now());
                """);
            migrationBuilder.DropCheckConstraint(
                name: "ck_media_items_type",
                table: "media_items");

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_items_type",
                table: "media_items",
                sql: "media_type IN ('movie', 'tv_show', 'album', 'song', 'video_game', 'book', 'custom')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE media_items, catalog_categories, catalog_category_aliases IN ACCESS EXCLUSIVE MODE;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM media_items WHERE media_type = 'song'
                        OR category_id = 'f11e0000-0000-0000-0000-000000000006')
                       OR EXISTS (SELECT 1 FROM catalog_category_aliases
                        WHERE source_category_id = 'f11e0000-0000-0000-0000-000000000006'
                           OR target_category_id = 'f11e0000-0000-0000-0000-000000000006')
                       OR EXISTS (SELECT 1 FROM catalog_categories WHERE id = 'f11e0000-0000-0000-0000-000000000006'
                        AND (NOT is_seeded OR is_hidden OR revision <> 1 OR name <> 'Songs'
                            OR normalized_name <> 'SONGS' OR scope <> 'public' OR owner_user_id IS NOT NULL)) THEN
                        RAISE EXCEPTION 'Cannot downgrade AddSongs: songs or Songs category usage/changes exist; review dependent content before rollback.';
                    END IF;
                END $$;
                DELETE FROM catalog_categories WHERE id = 'f11e0000-0000-0000-0000-000000000006';
                """);
            migrationBuilder.DropCheckConstraint(
                name: "ck_media_items_type",
                table: "media_items");

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_items_type",
                table: "media_items",
                sql: "media_type IN ('movie', 'tv_show', 'album', 'video_game', 'book', 'custom')");
        }
    }
}
