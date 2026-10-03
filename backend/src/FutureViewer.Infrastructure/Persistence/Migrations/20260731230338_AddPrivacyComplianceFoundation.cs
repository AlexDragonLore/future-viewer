using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FutureViewer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivacyComplianceFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "privacy_subject_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("UPDATE users SET privacy_subject_id = gen_random_uuid() WHERE privacy_subject_id IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "privacy_subject_id",
                table: "users",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "birth_year",
                table: "users",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE users SET birth_year = EXTRACT(YEAR FROM birth_date)::integer WHERE birth_date IS NOT NULL;");

            // Preserve the legacy profile column for existing records and rollback.
            // It is intentionally no longer mapped by the current application model.

            // The stored legacy value is the token sent by email. Hash it using
            // AuthService's SHA-256 UTF-8 format so already-issued links still work.
            // Skip hashes after a rollback/reapply; keep issuance/expiry dates intact.
            migrationBuilder.Sql("""
                UPDATE users
                SET email_verification_token = encode(sha256(convert_to(email_verification_token, 'UTF8')), 'hex')
                WHERE email_verification_token IS NOT NULL
                    AND email_verification_token !~ '^[0-9a-f]{64}$';

                UPDATE users
                SET password_reset_token = encode(sha256(convert_to(password_reset_token, 'UTF8')), 'hex')
                WHERE password_reset_token IS NOT NULL
                    AND password_reset_token !~ '^[0-9a-f]{64}$';
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "subject_reference",
                table: "processed_payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE processed_payments AS payment
                SET subject_reference = app_user.privacy_subject_id
                FROM users AS app_user
                WHERE payment.user_id = app_user.id;

                UPDATE processed_payments
                SET subject_reference = gen_random_uuid()
                WHERE subject_reference IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "subject_reference",
                table: "processed_payments",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "processed_payments");

            migrationBuilder.AddColumn<DateTime>(
                name: "account_deletion_requested_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "account_status",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "history_enabled",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_adult_confirmed",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "security_version",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "saved_to_history",
                table: "readings",
                type: "boolean",
                nullable: true);

            migrationBuilder.Sql("UPDATE readings SET saved_to_history = TRUE;");

            migrationBuilder.AlterColumn<bool>(
                name: "saved_to_history",
                table: "readings",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    actor_subject_reference = table.Column<Guid>(type: "uuid", nullable: true),
                    target_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    target_reference = table.Column<Guid>(type: "uuid", nullable: true),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reason_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    document_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "data_deletion_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_reference = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    scheduled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_deletion_jobs", x => x.id);
                    table.ForeignKey(
                        name: "FK_data_deletion_jobs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "data_subject_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_reference = table.Column<Guid>(type: "uuid", nullable: false),
                    request_type = table.Column<int>(type: "integer", nullable: false),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    identity_verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    due_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    result_reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_subject_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_data_subject_requests_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "legal_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_documents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_consents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_reference = table.Column<Guid>(type: "uuid", nullable: false),
                    consent_type = table.Column<int>(type: "integer", nullable: false),
                    legal_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    collection_source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ip_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_consents", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_consents_legal_documents_legal_document_id",
                        column: x => x.legal_document_id,
                        principalTable: "legal_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_consents_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_privacy_subject_id",
                table: "users",
                column: "privacy_subject_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_processed_payments_subject_reference",
                table: "processed_payments",
                column: "subject_reference");

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_actor_subject_reference",
                table: "audit_events",
                column: "actor_subject_reference");

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_event_type_occurred_at",
                table: "audit_events",
                columns: new[] { "event_type", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_occurred_at",
                table: "audit_events",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "IX_data_deletion_jobs_status_scheduled_at",
                table: "data_deletion_jobs",
                columns: new[] { "status", "scheduled_at" });

            migrationBuilder.CreateIndex(
                name: "IX_data_deletion_jobs_subject_reference",
                table: "data_deletion_jobs",
                column: "subject_reference");

            migrationBuilder.CreateIndex(
                name: "IX_data_deletion_jobs_user_id",
                table: "data_deletion_jobs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_data_subject_requests_status_due_at",
                table: "data_subject_requests",
                columns: new[] { "status", "due_at" });

            migrationBuilder.CreateIndex(
                name: "IX_data_subject_requests_subject_reference",
                table: "data_subject_requests",
                column: "subject_reference");

            migrationBuilder.CreateIndex(
                name: "IX_data_subject_requests_user_id",
                table: "data_subject_requests",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_legal_documents_document_type",
                table: "legal_documents",
                column: "document_type",
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_legal_documents_document_type_version",
                table: "legal_documents",
                columns: new[] { "document_type", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_consents_legal_document_id",
                table: "user_consents",
                column: "legal_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_consents_subject_reference",
                table: "user_consents",
                column: "subject_reference");

            migrationBuilder.CreateIndex(
                name: "IX_user_consents_user_id_consent_type",
                table: "user_consents",
                columns: new[] { "user_id", "consent_type" },
                unique: true,
                filter: "revoked_at IS NULL AND user_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "data_deletion_jobs");

            migrationBuilder.DropTable(
                name: "data_subject_requests");

            migrationBuilder.DropTable(
                name: "user_consents");

            migrationBuilder.DropTable(
                name: "legal_documents");

            migrationBuilder.DropIndex(
                name: "IX_users_privacy_subject_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_processed_payments_subject_reference",
                table: "processed_payments");

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                table: "processed_payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE processed_payments AS payment
                SET user_id = app_user.id
                FROM users AS app_user
                WHERE payment.subject_reference = app_user.privacy_subject_id;

                UPDATE processed_payments
                SET user_id = subject_reference
                WHERE user_id IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "processed_payments",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "subject_reference",
                table: "processed_payments");

            migrationBuilder.DropColumn(
                name: "account_deletion_requested_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "account_status",
                table: "users");

            migrationBuilder.DropColumn(
                name: "birth_year",
                table: "users");

            migrationBuilder.DropColumn(
                name: "history_enabled",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_adult_confirmed",
                table: "users");

            migrationBuilder.DropColumn(
                name: "privacy_subject_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "security_version",
                table: "users");

            migrationBuilder.DropColumn(
                name: "saved_to_history",
                table: "readings");

            // birth_date was retained by Up. Token hashing is intentionally
            // irreversible; a rollback to the old plaintext-token application
            // requires the pre-deployment backup to restore already-issued links.
        }
    }
}
