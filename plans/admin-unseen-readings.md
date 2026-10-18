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
- Frontend type-check/build, 354 unit tests, 16 desktop/mobile admin Playwright scenarios and backend/frontend technical checks passed.
- In-app browser inspected a synthetic admin page with personal unseen filtering and oldest-first sorting; responsive screenshots verified 320 px layout without overflow. No production admin session was used.
- The new receipt history starts with this release; past visits were not tracked and cannot be reconstructed.

## Production result — 18 October 2026, 20:27 MSK
- Published application release `87cae8e9ded3bdb2080180e0e3f1b6d546d50ec4` to main and deployed it successfully: https://github.com/AlexDragonLore/future-viewer/actions/runs/39148981010.
- Exact-release CI passed: 452 backend tests, 355 frontend unit tests, type-check/build and all 118 browser scenarios. Local focused admin/navigation browser checks passed; an additional broad local run was stopped after resource-related timeouts, and its six affected guest/legal cases passed with one worker. Full CI provides the completed broad browser result.
- Encrypted database backup, isolated restore, migration rehearsal, retained row counts, image digests, mounts/networks and unchanged neighboring services all verified. The only new migration is `20261018170408_AddAdminReadingViews`.
- Required base Compose ps succeeded. Public health returned status ok/database ready; the new acknowledgment endpoint requires admin authentication. Shipped assets contain both new filter labels and the acknowledgment endpoint.
- In-app production browser rendered the homepage with `/assets/index-BbZweU1H.js`. No production admin session was available; authenticated behavior was verified using synthetic browser fixtures and real PostgreSQL integration tests, including independent administrators.
- Protected recovery record: `/opt/fv-app/releases/admin-unseen-87cae8e9ded3bdb2080180e0e3f1b6d546d50ec4-20261018T172636Z-wNzHyf`.
