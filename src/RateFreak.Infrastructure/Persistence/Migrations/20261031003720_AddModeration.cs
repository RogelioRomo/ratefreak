using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RateFreak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_hidden",
                table: "reviews",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_hidden",
                table: "review_comments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "content_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_reporter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    review_id = table.Column<Guid>(type: "uuid", nullable: true),
                    comment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    resolution_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    resolved_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_reports", x => x.id);
                    table.CheckConstraint("ck_report_details", "(details IS NULL OR length(details) <= 1000) AND (reason <> 'other' OR (details IS NOT NULL AND length(btrim(details)) > 0))");
                    table.CheckConstraint("ck_report_reason", "reason IN ('spam','harassment','hate','sexual_content','violence','spoiler','other')");
                    table.CheckConstraint("ck_report_status", "(status = 'open' AND resolved_at IS NULL AND decision IS NULL AND resolution_reason IS NULL AND resolved_by IS NULL) OR (status = 'resolved' AND resolved_at IS NOT NULL AND decision IS NOT NULL AND decision IN ('hide','dismiss') AND resolution_reason IS NOT NULL AND resolved_by IS NOT NULL)");
                    table.CheckConstraint("ck_report_target", "target_id <> '00000000-0000-0000-0000-000000000000' AND ((target_type = 'review' AND comment_id IS NULL AND (review_id IS NULL OR review_id = target_id)) OR (target_type = 'comment' AND review_id IS NULL AND (comment_id IS NULL OR comment_id = target_id)))");
                    table.ForeignKey(
                        name: "FK_content_reports_review_comments_comment_id",
                        column: x => x.comment_id,
                        principalTable: "review_comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_content_reports_reviews_review_id",
                        column: x => x.review_id,
                        principalTable: "reviews",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_content_reports_users_reporter_id",
                        column: x => x.reporter_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "moderation_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    target_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    before = table.Column<bool>(type: "boolean", nullable: true),
                    after = table.Column<bool>(type: "boolean", nullable: true),
                    reviewed_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_moderation_events", x => x.id);
                    table.CheckConstraint("ck_moderation_reason", "length(btrim(reason)) > 0 AND length(reason) <= 1000");
                });

            migrationBuilder.CreateIndex(
                name: "IX_content_reports_comment_id",
                table: "content_reports",
                column: "comment_id");

            migrationBuilder.CreateIndex(
                name: "IX_content_reports_original_reporter_id_target_type_target_id",
                table: "content_reports",
                columns: new[] { "original_reporter_id", "target_type", "target_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_content_reports_reporter_id",
                table: "content_reports",
                column: "reporter_id");

            migrationBuilder.CreateIndex(
                name: "IX_content_reports_review_id",
                table: "content_reports",
                column: "review_id");

            migrationBuilder.CreateIndex(
                name: "IX_content_reports_status_created_at_id",
                table: "content_reports",
                columns: new[] { "status", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_moderation_events_created_at_id",
                table: "moderation_events",
                columns: new[] { "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_moderation_events_target_type_target_id_created_at_id",
                table: "moderation_events",
                columns: new[] { "target_type", "target_id", "created_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE reviews, review_comments, content_reports, moderation_events IN ACCESS EXCLUSIVE MODE;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM reviews WHERE is_hidden)
                       OR EXISTS (SELECT 1 FROM review_comments WHERE is_hidden)
                       OR EXISTS (SELECT 1 FROM content_reports)
                       OR EXISTS (SELECT 1 FROM moderation_events) THEN
                        RAISE EXCEPTION 'Cannot remove moderation while hidden content, reports or audit history exist.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "content_reports");

            migrationBuilder.DropTable(
                name: "moderation_events");

            migrationBuilder.DropColumn(
                name: "is_hidden",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "is_hidden",
                table: "review_comments");
        }
    }
}
