using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FutureViewer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrackIntroReadingEntitlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_used_intro_reading",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_reading_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            // Include retained, unsaved and removed-from-history records so existing
            // accounts cannot regain introductory access after deleting content.
            migrationBuilder.Sql("""
                UPDATE users AS u
                SET has_used_intro_reading = TRUE,
                    last_reading_at = usage.last_reading_at
                FROM (
                    SELECT user_id, MAX(created_at) AS last_reading_at
                    FROM readings
                    WHERE user_id IS NOT NULL
                    GROUP BY user_id
                ) AS usage
                WHERE u.id = usage.user_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "has_used_intro_reading",
                table: "users");

            migrationBuilder.DropColumn(
                name: "last_reading_at",
                table: "users");
        }
    }
}
