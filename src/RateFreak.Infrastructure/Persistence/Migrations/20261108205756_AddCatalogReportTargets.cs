using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RateFreak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogReportTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_report_target",
                table: "content_reports");

            migrationBuilder.AddColumn<Guid>(
                name: "category_id",
                table: "content_reports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "item_id",
                table: "content_reports",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_content_reports_category_id",
                table: "content_reports",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_content_reports_item_id",
                table: "content_reports",
                column: "item_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_report_target",
                table: "content_reports",
                sql: "target_id <> '00000000-0000-0000-0000-000000000000' AND ((target_type = 'review' AND comment_id IS NULL AND item_id IS NULL AND category_id IS NULL AND (review_id IS NULL OR review_id = target_id)) OR (target_type = 'comment' AND review_id IS NULL AND item_id IS NULL AND category_id IS NULL AND (comment_id IS NULL OR comment_id = target_id)) OR (target_type = 'item' AND review_id IS NULL AND comment_id IS NULL AND category_id IS NULL AND (item_id IS NULL OR item_id = target_id)) OR (target_type = 'category' AND review_id IS NULL AND comment_id IS NULL AND item_id IS NULL AND (category_id IS NULL OR category_id = target_id)))");

            migrationBuilder.AddForeignKey(
                name: "FK_content_reports_catalog_categories_category_id",
                table: "content_reports",
                column: "category_id",
                principalTable: "catalog_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_content_reports_media_items_item_id",
                table: "content_reports",
                column: "item_id",
                principalTable: "media_items",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM content_reports WHERE target_type IN ('item', 'category'))
                        OR EXISTS (SELECT 1 FROM moderation_events WHERE target_type IN ('item', 'category'))
                    THEN RAISE EXCEPTION 'Cannot downgrade catalog report targets while reports or audit history exist'; END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_content_reports_catalog_categories_category_id",
                table: "content_reports");

            migrationBuilder.DropForeignKey(
                name: "FK_content_reports_media_items_item_id",
                table: "content_reports");

            migrationBuilder.DropIndex(
                name: "IX_content_reports_category_id",
                table: "content_reports");

            migrationBuilder.DropIndex(
                name: "IX_content_reports_item_id",
                table: "content_reports");

            migrationBuilder.DropCheckConstraint(
                name: "ck_report_target",
                table: "content_reports");

            migrationBuilder.DropColumn(
                name: "category_id",
                table: "content_reports");

            migrationBuilder.DropColumn(
                name: "item_id",
                table: "content_reports");

            migrationBuilder.AddCheckConstraint(
                name: "ck_report_target",
                table: "content_reports",
                sql: "target_id <> '00000000-0000-0000-0000-000000000000' AND ((target_type = 'review' AND comment_id IS NULL AND (review_id IS NULL OR review_id = target_id)) OR (target_type = 'comment' AND review_id IS NULL AND (comment_id IS NULL OR comment_id = target_id)))");
        }
    }
}
