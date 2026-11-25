using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RateFreak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTmdbContentLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tmdb_lifecycle_states",
                columns: table => new
                {
                    mapping_id = table.Column<Guid>(type: "uuid", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failures = table.Column<int>(type: "integer", nullable: false),
                    last_outcome = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    content_removed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tmdb_lifecycle_states", x => x.mapping_id);
                    table.ForeignKey(
                        name: "FK_tmdb_lifecycle_states_media_external_ids_mapping_id",
                        column: x => x.mapping_id,
                        principalTable: "media_external_ids",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tmdb_poster_identities",
                columns: table => new
                {
                    mapping_id = table.Column<Guid>(type: "uuid", nullable: false),
                    filename = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tmdb_poster_identities", x => new { x.mapping_id, x.filename });
                    table.ForeignKey(
                        name: "FK_tmdb_poster_identities_media_external_ids_mapping_id",
                        column: x => x.mapping_id,
                        principalTable: "media_external_ids",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tmdb_refresh_schedule",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    last_batch_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_allowed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tmdb_refresh_schedule", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "tmdb_refresh_schedule",
                columns: new[] { "id", "last_batch_at", "next_allowed_at" },
                values: new object[] { 1, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_tmdb_lifecycle_states_next_attempt_at_mapping_id",
                table: "tmdb_lifecycle_states",
                columns: new[] { "next_attempt_at", "mapping_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tmdb_lifecycle_states");

            migrationBuilder.DropTable(
                name: "tmdb_poster_identities");

            migrationBuilder.DropTable(
                name: "tmdb_refresh_schedule");
        }
    }
}
