using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RateFreak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "collections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    visibility = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    schema_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collections", x => x.id);
                    table.CheckConstraint("ck_collections_name", "length(btrim(name)) > 0");
                    table.CheckConstraint("ck_collections_versions", "revision > 0 AND schema_version > 0");
                    table.CheckConstraint("ck_collections_visibility", "visibility IN ('private', 'public')");
                    table.ForeignKey(
                        name: "FK_collections_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "collection_fields",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    field_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    required = table.Column<bool>(type: "boolean", nullable: false),
                    configuration = table.Column<string>(type: "jsonb", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "collection_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    data = table.Column<string>(type: "jsonb", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_items", x => x.id);
                    table.CheckConstraint("ck_collection_items_data", "jsonb_typeof(data) = 'object'");
                    table.CheckConstraint("ck_collection_items_position", "position >= 0");
                    table.CheckConstraint("ck_collection_items_title", "(media_item_id IS NOT NULL AND title IS NULL) OR (media_item_id IS NULL AND title IS NOT NULL AND length(btrim(title)) > 0)");
                    table.ForeignKey(
                        name: "FK_collection_items_collections_collection_id",
                        column: x => x.collection_id,
                        principalTable: "collections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_collection_items_media_items_media_item_id",
                        column: x => x.media_item_id,
                        principalTable: "media_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_collection_fields_collection_id_position_id",
                table: "collection_fields",
                columns: new[] { "collection_id", "position", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_collection_fields_key",
                table: "collection_fields",
                columns: new[] { "collection_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_collection_items_collection_id_position_id",
                table: "collection_items",
                columns: new[] { "collection_id", "position", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_collection_items_media_item_id",
                table: "collection_items",
                column: "media_item_id");

            migrationBuilder.CreateIndex(
                name: "ux_collection_items_media",
                table: "collection_items",
                columns: new[] { "collection_id", "media_item_id" },
                unique: true,
                filter: "media_item_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_collections_owner_user_id_created_at_id",
                table: "collections",
                columns: new[] { "owner_user_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_collections_visibility_created_at_id",
                table: "collections",
                columns: new[] { "visibility", "created_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "collection_fields");

            migrationBuilder.DropTable(
                name: "collection_items");

            migrationBuilder.DropTable(
                name: "collections");
        }
    }
}
