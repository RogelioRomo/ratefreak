using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RateFreak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArtworkMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "artwork_assets",
                columns: table => new
                {
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    content_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    byte_length = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artwork_assets", x => x.item_id);
                    table.CheckConstraint("ck_artwork_dimensions", "width BETWEEN 1 AND 2048 AND height BETWEEN 1 AND 2048 AND byte_length > 0 AND content_type = 'image/webp'");
                    table.ForeignKey(
                        name: "FK_artwork_assets_media_items_item_id",
                        column: x => x.item_id,
                        principalTable: "media_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_artwork_assets_asset_id",
                table: "artwork_assets",
                column: "asset_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN IF EXISTS (SELECT 1 FROM artwork_assets)
                THEN RAISE EXCEPTION 'Cannot downgrade artwork metadata while assets exist'; END IF; END $$;
                """);
            migrationBuilder.DropTable(
                name: "artwork_assets");
        }
    }
}
