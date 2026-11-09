using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RateFreak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConvertCollectionsToLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM collection_items) OR EXISTS (SELECT 1 FROM collection_fields)
                    THEN RAISE EXCEPTION 'List cutover requires explicitly scoped offline test collection data clearing; incompatible entries or fields remain'; END IF;
                END $$;
                UPDATE collections SET revision = revision + 1, updated_at = CURRENT_TIMESTAMP;
                """);
            migrationBuilder.DropTable(
                name: "collection_fields");

            migrationBuilder.DropCheckConstraint(
                name: "ck_collections_versions",
                table: "collections");

            migrationBuilder.DropIndex(
                name: "ux_collection_items_media",
                table: "collection_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_collection_items_data",
                table: "collection_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_collection_items_title",
                table: "collection_items");

            migrationBuilder.DropColumn(
                name: "schema_version",
                table: "collections");

            migrationBuilder.DropColumn(
                name: "data",
                table: "collection_items");

            migrationBuilder.DropColumn(
                name: "title",
                table: "collection_items");

            migrationBuilder.AlterColumn<Guid>(
                name: "media_item_id",
                table: "collection_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_collections_versions",
                table: "collections",
                sql: "revision > 0");

            migrationBuilder.CreateIndex(
                name: "ux_collection_items_media",
                table: "collection_items",
                columns: new[] { "collection_id", "media_item_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM collection_items)
                    THEN RAISE EXCEPTION 'Cannot downgrade list schema while memberships exist; deleted historical field values cannot be recovered'; END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "ck_collections_versions",
                table: "collections");

            migrationBuilder.DropIndex(
                name: "ux_collection_items_media",
                table: "collection_items");

            migrationBuilder.AddColumn<long>(
                name: "schema_version",
                table: "collections",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AlterColumn<Guid>(
                name: "media_item_id",
                table: "collection_items",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "data",
                table: "collection_items",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "title",
                table: "collection_items",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "collection_fields",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuration = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    field_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    required = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_fields", x => x.id);
                    table.CheckConstraint("ck_collection_fields_configuration", "jsonb_typeof(configuration) = 'object'");
                    table.CheckConstraint("ck_collection_fields_key", "key ~ '^[a-z][a-z0-9_]{0,39}$' AND key NOT IN ('id','title','media_item_id','created_at','updated_at')");
                    table.CheckConstraint("ck_collection_fields_label", "length(btrim(label)) > 0");
                    table.CheckConstraint("ck_collection_fields_position", "position >= 0");
                    table.CheckConstraint("ck_collection_fields_type", "field_type IN ('text', 'number', 'boolean', 'date', 'select')");
                    table.ForeignKey(
                        name: "FK_collection_fields_collections_collection_id",
                        column: x => x.collection_id,
                        principalTable: "collections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_collections_versions",
                table: "collections",
                sql: "revision > 0 AND schema_version > 0");

            migrationBuilder.CreateIndex(
                name: "ux_collection_items_media",
                table: "collection_items",
                columns: new[] { "collection_id", "media_item_id" },
                unique: true,
                filter: "media_item_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_collection_items_data",
                table: "collection_items",
                sql: "jsonb_typeof(data) = 'object'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_collection_items_title",
                table: "collection_items",
                sql: "(media_item_id IS NOT NULL AND title IS NULL) OR (media_item_id IS NULL AND title IS NOT NULL AND length(btrim(title)) > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_collection_fields_collection_id_position_id",
                table: "collection_fields",
                columns: new[] { "collection_id", "position", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_collection_fields_key",
                table: "collection_fields",
                columns: new[] { "collection_id", "key" },
                unique: true);
        }
    }
}
