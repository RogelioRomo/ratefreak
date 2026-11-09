using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RateFreak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomCatalogFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_media_items_type",
                table: "media_items");

            migrationBuilder.AddColumn<Guid>(
                name: "category_id",
                table: "media_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "creator_user_id",
                table: "media_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_archived",
                table: "media_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_hidden",
                table: "media_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "provenance",
                table: "media_items",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "system");

            migrationBuilder.AddColumn<long>(
                name: "revision",
                table: "media_items",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<string>(
                name: "visibility",
                table: "media_items",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "public");

            migrationBuilder.CreateTable(
                name: "catalog_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    scope = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_hidden = table.Column<bool>(type: "boolean", nullable: false),
                    is_seeded = table.Column<bool>(type: "boolean", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_categories", x => x.id);
                    table.CheckConstraint("ck_category_name", "length(btrim(name)) > 0 AND length(btrim(normalized_name)) > 0");
                    table.CheckConstraint("ck_category_revision", "revision > 0");
                    table.CheckConstraint("ck_category_scope", "scope IN ('public', 'private') AND (scope <> 'private' OR owner_user_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_catalog_categories_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "catalog_category_aliases",
                columns: table => new
                {
                    source_category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_category_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_category_aliases", x => x.source_category_id);
                    table.CheckConstraint("ck_category_alias_distinct", "source_category_id <> target_category_id");
                    table.ForeignKey(
                        name: "FK_catalog_category_aliases_catalog_categories_source_category~",
                        column: x => x.source_category_id,
                        principalTable: "catalog_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_catalog_category_aliases_catalog_categories_target_category~",
                        column: x => x.target_category_id,
                        principalTable: "catalog_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO catalog_categories (id, name, normalized_name, scope, is_hidden, is_seeded, revision, created_at, updated_at)
                VALUES
                ('f11e0000-0000-0000-0000-000000000001', 'Movies', 'MOVIES', 'public', false, true, 1, now(), now()),
                ('f11e0000-0000-0000-0000-000000000002', 'TV shows', 'TV SHOWS', 'public', false, true, 1, now(), now()),
                ('f11e0000-0000-0000-0000-000000000003', 'Albums', 'ALBUMS', 'public', false, true, 1, now(), now()),
                ('f11e0000-0000-0000-0000-000000000004', 'Video games', 'VIDEO GAMES', 'public', false, true, 1, now(), now());
                UPDATE media_items SET category_id = CASE media_type
                    WHEN 'movie' THEN 'f11e0000-0000-0000-0000-000000000001'::uuid
                    WHEN 'tv_show' THEN 'f11e0000-0000-0000-0000-000000000002'::uuid
                    WHEN 'album' THEN 'f11e0000-0000-0000-0000-000000000003'::uuid
                    WHEN 'video_game' THEN 'f11e0000-0000-0000-0000-000000000004'::uuid END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_media_items_category_id_title_id",
                table: "media_items",
                columns: new[] { "category_id", "title", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_media_items_creator_user_id_id",
                table: "media_items",
                columns: new[] { "creator_user_id", "id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_items_provenance",
                table: "media_items",
                sql: "(provenance = 'system' AND creator_user_id IS NULL AND media_type <> 'custom' AND visibility = 'public') OR (provenance = 'user' AND creator_user_id IS NOT NULL AND media_type = 'custom')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_items_revision",
                table: "media_items",
                sql: "revision > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_items_type",
                table: "media_items",
                sql: "media_type IN ('movie', 'tv_show', 'album', 'video_game', 'custom')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_items_visibility",
                table: "media_items",
                sql: "visibility IN ('public', 'private')");

            migrationBuilder.CreateIndex(
                name: "ux_category_private_name",
                table: "catalog_categories",
                columns: new[] { "owner_user_id", "normalized_name" },
                unique: true,
                filter: "scope = 'private'");

            migrationBuilder.CreateIndex(
                name: "ux_category_public_name",
                table: "catalog_categories",
                column: "normalized_name",
                unique: true,
                filter: "scope = 'public'");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_category_aliases_target_category_id",
                table: "catalog_category_aliases",
                column: "target_category_id");

            migrationBuilder.AddForeignKey(
                name: "FK_media_items_catalog_categories_category_id",
                table: "media_items",
                column: "category_id",
                principalTable: "catalog_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_media_items_users_creator_user_id",
                table: "media_items",
                column: "creator_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM media_items WHERE media_type = 'custom' OR visibility <> 'public' OR is_archived OR is_hidden)
                       OR EXISTS (SELECT 1 FROM catalog_categories WHERE NOT is_seeded OR is_hidden)
                       OR EXISTS (SELECT 1 FROM catalog_category_aliases)
                    THEN RAISE EXCEPTION 'Cannot downgrade catalog foundation while custom/private content or category changes exist';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_media_items_catalog_categories_category_id",
                table: "media_items");

            migrationBuilder.DropForeignKey(
                name: "FK_media_items_users_creator_user_id",
                table: "media_items");

            migrationBuilder.DropTable(
                name: "catalog_category_aliases");

            migrationBuilder.DropTable(
                name: "catalog_categories");

            migrationBuilder.DropIndex(
                name: "IX_media_items_category_id_title_id",
                table: "media_items");

            migrationBuilder.DropIndex(
                name: "IX_media_items_creator_user_id_id",
                table: "media_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_media_items_provenance",
                table: "media_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_media_items_revision",
                table: "media_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_media_items_type",
                table: "media_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_media_items_visibility",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "category_id",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "creator_user_id",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "is_archived",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "is_hidden",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "provenance",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "revision",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "visibility",
                table: "media_items");

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_items_type",
                table: "media_items",
                sql: "media_type IN ('movie', 'tv_show', 'album', 'video_game')");
        }
    }
}
