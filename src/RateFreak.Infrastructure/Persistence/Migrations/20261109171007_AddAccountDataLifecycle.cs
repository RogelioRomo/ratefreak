using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RateFreak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountDataLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_media_items_provenance",
                table: "media_items");

            migrationBuilder.AlterColumn<Guid>(
                name: "original_author_id",
                table: "content_reports",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "account_data_grants",
                columns: table => new
                {
                    hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    security_stamp = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    operation = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_data_grants", x => x.hash);
                    table.ForeignKey(
                        name: "FK_account_data_grants_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "account_export_budgets",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    window_ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_export_budgets", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_account_export_budgets_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_items_provenance",
                table: "media_items",
                sql: "(provenance = 'system' AND creator_user_id IS NULL AND media_type <> 'custom' AND visibility = 'public') OR (provenance = 'user' AND (creator_user_id IS NOT NULL OR visibility = 'public') AND media_type = 'custom')");

            migrationBuilder.CreateIndex(
                name: "IX_account_data_grants_expires_at",
                table: "account_data_grants",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_account_data_grants_user_id",
                table: "account_data_grants",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Ownership cannot be reconstructed after erasure. Refuse unsafe rollback rather than fabricate accounts.
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM media_items WHERE provenance = 'user' AND creator_user_id IS NULL) OR EXISTS (SELECT 1 FROM content_reports WHERE original_author_id IS NULL) THEN RAISE EXCEPTION 'Account lifecycle rollback requires restoring a pre-erasure backup and replaying erasures before serving traffic.'; END IF; END $$;");
            migrationBuilder.DropTable(
                name: "account_data_grants");

            migrationBuilder.DropTable(
                name: "account_export_budgets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_media_items_provenance",
                table: "media_items");

            migrationBuilder.AlterColumn<Guid>(
                name: "original_author_id",
                table: "content_reports",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_items_provenance",
                table: "media_items",
                sql: "(provenance = 'system' AND creator_user_id IS NULL AND media_type <> 'custom' AND visibility = 'public') OR (provenance = 'user' AND creator_user_id IS NOT NULL AND media_type = 'custom')");
        }
    }
}
