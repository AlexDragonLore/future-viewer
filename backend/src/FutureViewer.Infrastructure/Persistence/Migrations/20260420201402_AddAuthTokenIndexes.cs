using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FutureViewer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthTokenIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_users_email_verification_token",
                table: "users",
                column: "email_verification_token");

            migrationBuilder.CreateIndex(
                name: "IX_users_password_reset_token",
                table: "users",
                column: "password_reset_token");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_email_verification_token",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_password_reset_token",
                table: "users");
        }
    }
}
