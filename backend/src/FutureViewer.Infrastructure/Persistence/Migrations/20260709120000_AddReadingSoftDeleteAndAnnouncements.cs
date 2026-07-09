using System;
using FutureViewer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FutureViewer.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260709120000_AddReadingSoftDeleteAndAnnouncements")]
    public partial class AddReadingSoftDeleteAndAnnouncements : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_from_history_at",
                table: "readings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "announcements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_announcements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "announcement_reads",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    announcement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_announcement_reads", x => x.id);
                    table.ForeignKey(
                        name: "FK_announcement_reads_announcements_announcement_id",
                        column: x => x.announcement_id,
                        principalTable: "announcements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_announcement_reads_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_announcements_code",
                table: "announcements",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_announcements_published_at",
                table: "announcements",
                column: "published_at");

            migrationBuilder.CreateIndex(
                name: "IX_announcement_reads_announcement_id",
                table: "announcement_reads",
                column: "announcement_id");

            migrationBuilder.CreateIndex(
                name: "IX_announcement_reads_user_id_announcement_id",
                table: "announcement_reads",
                columns: new[] { "user_id", "announcement_id" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "announcement_reads");
            migrationBuilder.DropTable(name: "announcements");
            migrationBuilder.DropColumn(name: "deleted_from_history_at", table: "readings");
        }
    }
}
