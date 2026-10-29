using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RateFreak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonicalCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "media_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    release_date = table.Column<DateOnly>(type: "date", nullable: true),
                    image_reference = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_items", x => x.id);
                    table.CheckConstraint("ck_media_items_metadata", "jsonb_typeof(metadata) = 'object'");
                    table.CheckConstraint("ck_media_items_title", "length(btrim(title)) > 0");
                    table.CheckConstraint("ck_media_items_type", "media_type IN ('movie', 'tv_show')");
                });

            migrationBuilder.CreateTable(
                name: "media_external_ids",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    @namespace = table.Column<string>(name: "namespace", type: "character varying(50)", maxLength: 50, nullable: false),
                    external_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    external_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    last_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_external_ids", x => x.id);
                    table.CheckConstraint("ck_media_external_ids_id", "length(btrim(external_id)) > 0");
                    table.CheckConstraint("ck_media_external_ids_namespace", "namespace ~ '^[a-z0-9_-]+$'");
                    table.CheckConstraint("ck_media_external_ids_provider", "provider ~ '^[a-z0-9_-]+$'");
                    table.ForeignKey(
                        name: "FK_media_external_ids_media_items_media_item_id",
                        column: x => x.media_item_id,
                        principalTable: "media_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_media_external_ids_media_item_id",
                table: "media_external_ids",
                column: "media_item_id");

            migrationBuilder.CreateIndex(
                name: "ux_media_external_ids_identity",
                table: "media_external_ids",
                columns: new[] { "provider", "namespace", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_media_items_title_id",
                table: "media_items",
                columns: new[] { "title", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_media_items_type_title_id",
                table: "media_items",
                columns: new[] { "media_type", "title", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "media_external_ids");

            migrationBuilder.DropTable(
                name: "media_items");
        }
    }
}
