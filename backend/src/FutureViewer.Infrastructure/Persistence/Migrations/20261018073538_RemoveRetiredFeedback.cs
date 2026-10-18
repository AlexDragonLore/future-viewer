using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FutureViewer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRetiredFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Feedback and its four achievements were explicitly retired together.
            // Deleting these catalog rows cascades only their associated grants.
            migrationBuilder.Sql("""
                DELETE FROM achievements
                WHERE code IN ('first_feedback', 'score_master', 'perfect_10', 'high_five');
                """);
            migrationBuilder.DropTable(
                name: "reading_feedbacks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreates the schema only. Deleted answers and achievement grants
            // require restoring the pre-deployment database backup.
            migrationBuilder.CreateTable(
                name: "reading_feedbacks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reading_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ai_score = table.Column<int>(type: "integer", nullable: true),
                    ai_score_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    answered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_sincere = table.Column<bool>(type: "boolean", nullable: true),
                    notified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    scheduled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    self_report = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reading_feedbacks", x => x.id);
                    table.ForeignKey(
                        name: "FK_reading_feedbacks_readings_reading_id",
                        column: x => x.reading_id,
                        principalTable: "readings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_reading_feedbacks_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reading_feedbacks_reading_id",
                table: "reading_feedbacks",
                column: "reading_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reading_feedbacks_scheduled_at",
                table: "reading_feedbacks",
                column: "scheduled_at");

            migrationBuilder.CreateIndex(
                name: "IX_reading_feedbacks_token",
                table: "reading_feedbacks",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reading_feedbacks_user_id_status",
                table: "reading_feedbacks",
                columns: new[] { "user_id", "status" });
        }
    }
}
