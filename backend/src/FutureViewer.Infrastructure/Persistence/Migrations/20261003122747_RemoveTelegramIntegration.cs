using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FutureViewer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTelegramIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve historical evidence, but retire all active permissions for this integration.
            migrationBuilder.Sql("UPDATE user_consents SET revoked_at = CURRENT_TIMESTAMP WHERE consent_type = 11 AND revoked_at IS NULL;");
            // Grants cascade with the retired catalog entry.
            migrationBuilder.Sql("DELETE FROM achievements WHERE code = 'telegram_linked';");

            migrationBuilder.DropIndex(
                name: "IX_users_telegram_chat_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "telegram_chat_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "telegram_link_token",
                table: "users");

            migrationBuilder.DropColumn(
                name: "telegram_link_token_expires_at",
                table: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "telegram_chat_id",
                table: "users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "telegram_link_token",
                table: "users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "telegram_link_token_expires_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_telegram_chat_id",
                table: "users",
                column: "telegram_chat_id",
                unique: true);
        }
    }
}
