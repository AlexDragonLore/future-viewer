using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FutureViewer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramLinkTokenExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "telegram_link_token_expires_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            // Existing link tokens had no provable issuance time. Invalidate them
            // instead of assigning an invented lifetime during migration.
            migrationBuilder.Sql("UPDATE users SET telegram_link_token = NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "telegram_link_token_expires_at",
                table: "users");
        }
    }
}
