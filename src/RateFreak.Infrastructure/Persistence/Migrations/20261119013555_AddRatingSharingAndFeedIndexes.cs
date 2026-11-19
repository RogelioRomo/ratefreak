using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RateFreak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRatingSharingAndFeedIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_shared",
                table: "ratings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "shared_at",
                table: "ratings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_reviews_created_at_user_id_media_item_id",
                table: "reviews",
                columns: new[] { "created_at", "user_id", "media_item_id" },
                filter: "NOT is_hidden");

            migrationBuilder.CreateIndex(
                name: "IX_ratings_shared_at_user_id_media_item_id",
                table: "ratings",
                columns: new[] { "shared_at", "user_id", "media_item_id" },
                filter: "is_shared");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ratings_sharing",
                table: "ratings",
                sql: "(is_shared AND shared_at IS NOT NULL) OR (NOT is_shared AND shared_at IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_reviews_created_at_user_id_media_item_id",
                table: "reviews");

            migrationBuilder.DropIndex(
                name: "IX_ratings_shared_at_user_id_media_item_id",
                table: "ratings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ratings_sharing",
                table: "ratings");

            migrationBuilder.DropColumn(
                name: "is_shared",
                table: "ratings");

            migrationBuilder.DropColumn(
                name: "shared_at",
                table: "ratings");
        }
    }
}
