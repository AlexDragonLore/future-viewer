# Personal unseen admin messages and chronological sorting

## Implementation
- Work from current origin/main in an isolated worktree; preserve unrelated local changes.
- Persist per-admin/per-reading view receipts with unique keys and cascading deletion. Derive the admin exclusively from authenticated claims.
- Add unread-only filtering and stable oldest/newest ordering, combining existing search and audience filters.
- A successfully rendered list page acknowledges every message on that page. GET remains read-only; failed/stale/unmounted loads do not acknowledge unseen content. Repeated acknowledgments are idempotent.
- Keep the displayed unread batch visible after acknowledgment. Load the next batch from the head of the remaining unread queue so shrinking results cannot skip records. Errors remain visible and retryable.

## QA / verification
- Backend full solution tests with Docker, including two-admin isolation, authorization, idempotent concurrent receipt insertion, retained-content scope, pagination and stable chronology; verify migration forward/backward.
- Frontend type-check, unit tests, build, desktop/mobile Playwright browser scenarios for acknowledgment, unread batches, filtering, sorting, retry, stale loads and account reset.
- Inspect rendered admin UI through Browser Use/in-app browser if available; otherwise inspect Playwright screenshots. Production public browser smoke; if no authenticated production session is available, state that limitation and validate admin using local synthetic fixtures plus production protected endpoint/schema checks.

## Deploy
- Commit this change and push tested release to origin/main, reconciling concurrent updates before publication.
- Use existing CI delivery identity when direct SSH is unavailable. Pin source/build to tested SHA. Production host deploy@80.87.104.245; checkout /opt/future-viewer; environment /opt/fv-app/.env.production.
- Preserve production settings; record prior app images and revision, take and restore-test an encrypted database backup before applying the additive migration. Build linux/amd64 images and verify transfer checksums/digests.
- Fast-forward server checkout to release SHA. Update backend/frontend only with: sudo -n env FV_IMAGE_TAG=<sha> FV_SHARED_EDGE_PORT=18080 FV_LEGACY_EDGE_IP=<verified-current-ip> docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-deps --no-build --pull never backend frontend.
- Verify docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps, GET https://alex-taro.ru/health, exact images, new migration, protected endpoints, shipped frontend assets, production browser smoke and unchanged neighboring services. Roll back application images if verification fails; preserve data.

## Local verification result
- 452 backend tests passed (10 domain, 181 domain services, 261 integration), including the new migration up/down and cascading deletion checks.
- Frontend type-check/build, 355 unit tests, 16 desktop/mobile admin Playwright scenarios and backend/frontend technical checks passed.
- In-app browser inspected a synthetic admin page with personal unseen filtering and oldest-first sorting; responsive screenshots verified 320 px layout without overflow. No production admin session was used.
- The new receipt history starts with this release; past visits were not tracked and cannot be reconstructed.

- Final review added an explicit successful-load revision so returning to an already cached page also acknowledges it. Type-check/build, all 355 unit tests and 16 admin browser scenarios passed again.
- Full GitHub browser CI exposed a second admin fixture in the SEO navigation scenario that rejected the new acknowledgment POST. Updated it to verify the authenticated request and the exact displayed IDs; both desktop/mobile scenarios and type-check passed. The failed delivery stopped at its test gate before obtaining deployment credentials or changing production.
