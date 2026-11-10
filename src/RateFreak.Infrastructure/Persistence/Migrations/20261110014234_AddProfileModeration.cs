using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RateFreak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_report_target",
                table: "content_reports");

            migrationBuilder.AddColumn<bool>(
                name: "is_profile_hidden",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "profile_updated_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<Guid>(
                name: "profile_id",
                table: "content_reports",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_content_reports_profile_id",
                table: "content_reports",
                column: "profile_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_report_target",
                table: "content_reports",
                sql: "target_id <> '00000000-0000-0000-0000-000000000000' AND ((target_type = 'profile' AND review_id IS NULL AND comment_id IS NULL AND item_id IS NULL AND category_id IS NULL AND (profile_id IS NULL OR profile_id = target_id)) OR (profile_id IS NULL AND ((target_type = 'review' AND comment_id IS NULL AND item_id IS NULL AND category_id IS NULL AND (review_id IS NULL OR review_id = target_id)) OR (target_type = 'comment' AND review_id IS NULL AND item_id IS NULL AND category_id IS NULL AND (comment_id IS NULL OR comment_id = target_id)) OR (target_type = 'item' AND review_id IS NULL AND comment_id IS NULL AND category_id IS NULL AND (item_id IS NULL OR item_id = target_id)) OR (target_type = 'category' AND review_id IS NULL AND comment_id IS NULL AND item_id IS NULL AND (category_id IS NULL OR category_id = target_id)))))");

            migrationBuilder.AddForeignKey(
                name: "FK_content_reports_users_profile_id",
                table: "content_reports",
                column: "profile_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE users, content_reports, moderation_events IN ACCESS EXCLUSIVE MODE;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM users WHERE is_profile_hidden)
                        OR EXISTS (SELECT 1 FROM content_reports WHERE target_type = 'profile')
                        OR EXISTS (SELECT 1 FROM moderation_events WHERE target_type = 'profile') THEN
                        RAISE EXCEPTION 'Profile moderation rollback refused: hidden profiles, reports or audit history must be preserved.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_content_reports_users_profile_id",
                table: "content_reports");

            migrationBuilder.DropIndex(
                name: "IX_content_reports_profile_id",
                table: "content_reports");

            migrationBuilder.DropCheckConstraint(
                name: "ck_report_target",
                table: "content_reports");

            migrationBuilder.DropColumn(
                name: "is_profile_hidden",
                table: "users");

            migrationBuilder.DropColumn(
                name: "profile_updated_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "profile_id",
                table: "content_reports");

            migrationBuilder.AddCheckConstraint(
                name: "ck_report_target",
                table: "content_reports",
                sql: "target_id <> '00000000-0000-0000-0000-000000000000' AND ((target_type = 'review' AND comment_id IS NULL AND item_id IS NULL AND category_id IS NULL AND (review_id IS NULL OR review_id = target_id)) OR (target_type = 'comment' AND review_id IS NULL AND item_id IS NULL AND category_id IS NULL AND (comment_id IS NULL OR comment_id = target_id)) OR (target_type = 'item' AND review_id IS NULL AND comment_id IS NULL AND category_id IS NULL AND (item_id IS NULL OR item_id = target_id)) OR (target_type = 'category' AND review_id IS NULL AND comment_id IS NULL AND item_id IS NULL AND (category_id IS NULL OR category_id = target_id)))");
        }
    }
}
