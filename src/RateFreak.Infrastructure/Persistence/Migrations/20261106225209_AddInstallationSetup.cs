using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RateFreak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInstallationSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "installation_state",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    initial_admin_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_installation_state", x => x.id);
                    table.CheckConstraint("ck_installation_singleton", "id = 1");
                });
            migrationBuilder.Sql("""
                INSERT INTO installation_state (id, completed_at, initial_admin_id)
                SELECT 1, CASE WHEN EXISTS (SELECT 1 FROM users) THEN CURRENT_TIMESTAMP ELSE NULL END, NULL;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "installation_state");
        }
    }
}
