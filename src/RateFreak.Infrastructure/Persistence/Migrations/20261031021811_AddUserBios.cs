using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RateFreak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserBios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "bio",
                table: "users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_bio",
                table: "users",
                sql: "bio IS NULL OR (length(btrim(bio, ' ' || chr(9) || chr(10) || chr(13))) > 0 AND length(bio) <= 500)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE users IN ACCESS EXCLUSIVE MODE;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM users WHERE bio IS NOT NULL) THEN
                        RAISE EXCEPTION 'Cannot remove profile bios while profile text exists.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "ck_users_bio",
                table: "users");

            migrationBuilder.DropColumn(
                name: "bio",
                table: "users");
        }
    }
}
