using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FutureViewer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnableHistoryByDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "history_enabled",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            // Upgrade only the old implicit default. History choices are audited using
            // privacy_subject_id, not users.id; preserve every explicit user choice.
            // Do not change blocked accounts, deletion requests, or any reading content.
            migrationBuilder.Sql("""
                UPDATE users AS app_user
                SET history_enabled = TRUE
                WHERE app_user.history_enabled = FALSE
                    AND app_user.account_status = 0
                    AND app_user.account_deletion_requested_at IS NULL
                    AND NOT EXISTS (
                        SELECT 1 FROM audit_events AS audit
                        WHERE audit.event_type = 'privacy.history_setting_changed'
                            AND (audit.actor_subject_reference = app_user.privacy_subject_id
                                OR (audit.target_type = 'user'
                                    AND audit.target_reference = app_user.privacy_subject_id)))
                    AND NOT EXISTS (
                        SELECT 1 FROM data_deletion_jobs AS deletion
                        WHERE deletion.subject_reference = app_user.privacy_subject_id
                            AND deletion.status IN (0, 1));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Preserve existing preferences when rolling back the schema default.
            migrationBuilder.AlterColumn<bool>(
                name: "history_enabled",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);
        }
    }
}
