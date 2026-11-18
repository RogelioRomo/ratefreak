using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RateFreak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    follow_actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    follow_recipient_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.CheckConstraint("ck_notifications_no_self", "actor_id <> recipient_id");
                    table.CheckConstraint("ck_notifications_source", "(kind = 'follow' AND comment_id IS NULL AND follow_actor_id IS NOT NULL AND follow_recipient_id IS NOT NULL AND follow_actor_id = actor_id AND follow_recipient_id = recipient_id) OR (kind = 'comment' AND comment_id IS NOT NULL AND follow_actor_id IS NULL AND follow_recipient_id IS NULL)");
                    table.ForeignKey(
                        name: "FK_notifications_review_comments_comment_id",
                        column: x => x.comment_id,
                        principalTable: "review_comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notifications_user_follows_follow_actor_id_follow_recipient~",
                        columns: x => new { x.follow_actor_id, x.follow_recipient_id },
                        principalTable: "user_follows",
                        principalColumns: new[] { "follower_id", "followee_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notifications_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notifications_users_recipient_id",
                        column: x => x.recipient_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_actor_id_recipient_id",
                table: "notifications",
                columns: new[] { "actor_id", "recipient_id" },
                unique: true,
                filter: "kind = 'follow'");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_comment_id",
                table: "notifications",
                column: "comment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_follow_actor_id_follow_recipient_id",
                table: "notifications",
                columns: new[] { "follow_actor_id", "follow_recipient_id" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_recipient_id_created_at",
                table: "notifications",
                columns: new[] { "recipient_id", "created_at" },
                filter: "read_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_recipient_id_created_at_id",
                table: "notifications",
                columns: new[] { "recipient_id", "created_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notifications");
        }
    }
}
