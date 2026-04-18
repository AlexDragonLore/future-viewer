using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FutureViewer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "subscription_expires_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "subscription_status",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "yukassa_subscription_id",
                table: "users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "subscription_expires_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "subscription_status",
                table: "users");

            migrationBuilder.DropColumn(
                name: "yukassa_subscription_id",
                table: "users");
        }
    }
}
