START TRANSACTION;
DELETE FROM achievements
WHERE code IN ('first_feedback', 'score_master', 'perfect_10', 'high_five');

DROP TABLE reading_feedbacks;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261018073538_RemoveRetiredFeedback', '10.0.10');

COMMIT;

