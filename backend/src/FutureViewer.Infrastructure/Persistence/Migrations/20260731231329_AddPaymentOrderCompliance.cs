using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FutureViewer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentOrderCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_reference = table.Column<Guid>(type: "uuid", nullable: false),
                    tariff_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    access_days = table.Column<int>(type: "integer", nullable: false),
                    provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    provider_payment_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    receipt_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    receipt_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    receipt_issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_orders_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payment_orders_provider_idempotency_key",
                table: "payment_orders",
                columns: new[] { "provider", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_orders_provider_payment_id",
                table: "payment_orders",
                column: "provider_payment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_orders_public_id",
                table: "payment_orders",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_orders_subject_reference",
                table: "payment_orders",
                column: "subject_reference");

            migrationBuilder.CreateIndex(
                name: "IX_payment_orders_user_id",
                table: "payment_orders",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_orders");
        }
    }
}
