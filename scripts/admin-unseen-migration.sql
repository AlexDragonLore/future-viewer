START TRANSACTION;
CREATE TABLE admin_reading_views (
    admin_id uuid NOT NULL,
    reading_id uuid NOT NULL,
    viewed_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_admin_reading_views" PRIMARY KEY (admin_id, reading_id),
    CONSTRAINT "FK_admin_reading_views_readings_reading_id" FOREIGN KEY (reading_id) REFERENCES readings (id) ON DELETE CASCADE,
    CONSTRAINT "FK_admin_reading_views_users_admin_id" FOREIGN KEY (admin_id) REFERENCES users (id) ON DELETE CASCADE
);

CREATE INDEX "IX_admin_reading_views_reading_id" ON admin_reading_views (reading_id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261018170408_AddAdminReadingViews', '10.0.10');

COMMIT;

