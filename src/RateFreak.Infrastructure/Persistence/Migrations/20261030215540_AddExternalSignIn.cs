using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RateFreak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalSignIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_logins_user_id",
                table: "user_logins");

            migrationBuilder.CreateTable(
                name: "external_authentication_challenges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_authentication_challenges", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_user_logins_user_provider",
                table: "user_logins",
                columns: new[] { "user_id", "login_provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_authentication_challenges_expires_at",
                table: "external_authentication_challenges",
                column: "expires_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "external_authentication_challenges");

            migrationBuilder.DropIndex(
                name: "ux_user_logins_user_provider",
                table: "user_logins");

            migrationBuilder.CreateIndex(
                name: "IX_user_logins_user_id",
                table: "user_logins",
                column: "user_id");
        }
    }
}
