using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FutureViewer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowRepeatedOfferAcceptance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_consents_user_id_consent_type",
                table: "user_consents");

            migrationBuilder.CreateIndex(
                name: "IX_user_consents_user_id_consent_type",
                table: "user_consents",
                columns: new[] { "user_id", "consent_type" },
                unique: true,
                filter: "revoked_at IS NULL AND user_id IS NOT NULL AND consent_type <> 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_consents_user_id_consent_type",
                table: "user_consents");

            migrationBuilder.CreateIndex(
                name: "IX_user_consents_user_id_consent_type",
                table: "user_consents",
                columns: new[] { "user_id", "consent_type" },
                unique: true,
                filter: "revoked_at IS NULL AND user_id IS NOT NULL");
        }
    }
}
