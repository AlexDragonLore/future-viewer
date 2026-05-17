using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FutureViewer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTarotPlusSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "tarot_plus_credits",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "tarot_plus_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    route = table.Column<int>(type: "integer", nullable: false),
                    core_request = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    preview_text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    answers_json = table.Column<string>(type: "jsonb", nullable: false),
                    selected_spreads_json = table.Column<string>(type: "jsonb", nullable: false),
                    drawn_cards_json = table.Column<string>(type: "jsonb", nullable: false),
                    report_markdown = table.Column<string>(type: "text", nullable: true),
                    ai_model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    followups_left = table.Column<int>(type: "integer", nullable: false),
                    followups_json = table.Column<string>(type: "jsonb", nullable: false),
                    payment_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    price_rub = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    safety_flags_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tarot_plus_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_tarot_plus_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tarot_plus_sessions_payment_id",
                table: "tarot_plus_sessions",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_tarot_plus_sessions_user_id",
                table: "tarot_plus_sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_tarot_plus_sessions_user_id_created_at",
                table: "tarot_plus_sessions",
                columns: new[] { "user_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tarot_plus_sessions");

            migrationBuilder.DropColumn(
                name: "tarot_plus_credits",
                table: "users");
        }
    }
}
