using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RateFreak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileAvatars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "avatar_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "avatar_revision",
                table: "users",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "avatar_assets",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    content_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    byte_length = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_avatar_assets", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_avatar_assets_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_avatar_assets_asset_id",
                table: "avatar_assets",
                column: "asset_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM avatar_assets) OR EXISTS (SELECT 1 FROM users WHERE avatar_id IS NOT NULL) THEN
                        RAISE EXCEPTION 'Cannot remove avatar support while avatar data exists. Remove avatars through the application before downgrade.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "avatar_assets");

            migrationBuilder.DropColumn(
                name: "avatar_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "avatar_revision",
                table: "users");
        }
    }
}
