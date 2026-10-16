# Site performance fixes and calmer compact admin styling

## Scope

- Show restored completed interpretations immediately; render genuine streaming Markdown at a bounded cadence with an immediate final flush and unchanged sanitization/manual-scroll behavior.
- Deduplicate concurrent subscription refreshes within one account/session while preserving forced refresh after purchases/readings and rejecting stale responses after logout/token changes.
- Add an owner-scoped, paginated history summary endpoint and use it in the frontend; avoid loading full interpretations/cards for preview rows while preserving the existing full-detail and legacy history APIs.
- Skip profile/memory database reads whose context is intentionally discarded by the interpreter; preserve that privacy boundary, authorization and quota checks.
- Reuse achievement catalog/ownership data and aggregate feedback statistics in SQL; preserve unique/concurrent achievement grants. Remove quadratic payment matching in privacy export without truncating exported data, and bound cleanup batches while preserving retention semantics.
- Remove the artificial route-exit delay and separate the heavy tarot SEO catalog/JSON-LD builder from the initial application path while keeping metadata consistent with generated static pages.
- Refine compact administrator styling based on the supplied mobile screenshot: calmer row surfaces and borders, consistent typography/icon alignment, a unified search control, and a distinct muted-blue guest label. Display every question in full by default. Keep AI answers collapsed until requested, compact subscription icons, full details on demand and pagination scroll-to-top.
- Work in the existing clean release worktree based on `origin/main`; preserve unrelated changes in the original checkout. No payment-flow or feedback-removal release is included.

## QA / verification

- Run focused tests for completed/restored and streaming interpretations, subscription request deduplication/forced refresh/session races, summary pagination/ownership/query shape, achievement query counts/grant races, export completeness and cleanup batch boundaries.
- Run full `dotnet test backend/FutureViewer.slnx` with Docker after backend edits settle; frontend `npm run type-check`, `npm test`, `npm run build`; technical compliance and independent diff review.
- Verify SEO navigation races, noindex/canonical/JSON-LD correctness and static generator output; inspect actual bundle contents/sizes to confirm the heavy catalog is no longer in the initial path.
- Use Browser Use/in-app browser for rendered local QA when available. If no browser is enabled, use repository Playwright scenarios and inspect screenshots at desktop and mobile widths, including the admin styling, guest label, history pagination, ready/restored answer and progressive stream.
- Use synthetic fixtures and local databases; do not create production accounts/readings/payments for QA. Distinguish code/query-count proof and local benchmarks from production timing.

## Deploy

- Target `main` at `git@github.com:AlexDragonLore/future-viewer.git`. Commit the tested scope, fetch/reconcile concurrent changes, then `git push origin HEAD:main`.
- Production: `deploy@80.87.104.245`, checkout `/opt/future-viewer`, protected env `/opt/fv-app/.env.production`, Compose project `future-viewer`. Use the existing deployment identity through a temporary GitHub Actions delivery branch if local SSH is still unavailable.
- Pin builds to the tested SHA; preserve public frontend build settings by filtering them on the server before transfer. Check image archive SHA-256 and exported configuration digests, then use verified server-local Docker IDs.
- Record separate current frontend/backend images, Git revision, environment, Caddy and persistent resources. Verify an encrypted database backup with an isolated restore before backend replacement; compare migrations explicitly. No schema migration is expected.
- Fast-forward server checkout using `git fetch origin main` and `git merge --ff-only <release-sha>`; update only `FV_IMAGE_TAG` in the existing protected environment.
- Update command: `sudo -n env FV_IMAGE_TAG=<release-sha> FV_SHARED_EDGE_PORT=18080 FV_LEGACY_EDGE_IP=<verified-caddy-ip> docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-deps --no-build --pull never backend frontend`.
- Verify required base Compose `ps`, `GET https://alex-taro.ru/health`, exact images, protected endpoint behavior, shipped assets, production browser smoke and unchanged neighboring services including `janetka.ru`. Restore saved application images/configuration on failed verification without restoring the live database or removing volumes.

## Verification results before deployment

- Full backend suite: 460 passed (10 Domain, 195 DomainServices, 255 integration), no failures or skips. Full frontend suite: 360 passed across 44 files; type-check and production build passed. Technical compliance checks and independent code review passed; existing documentary metadata warnings remain.
- Rendered browser QA used Playwright because Browser Use reported no enabled browsers. Admin: six existing scenarios plus desktop/390/320 px visual checks; full questions, distinct guest color, lazy answer request, purchase details, no horizontal overflow and pagination scroll offset verified. Final sans-serif heading adjustment was rebuilt and inspected at all three widths.
- Result/subscription: six rendered scenarios cover immediate restored content, final stream flush and manual scroll position. History: two rendered scenarios cover pagination at 320/393 px. SEO: two built-preview scenarios verify client navigation home → auth → FAQ → private admin, catalog deferred until FAQ, correct canonical/JSON-LD and complete private-page metadata clearing.
- Initial JS is 301.64 kB (111.79 kB gzip); the 47.13 kB tarot catalog is a separate lazy chunk. Achievement reads fall from eight to six SELECTs excluding JWT authentication. Cleanup uses bounded batches with its time budget checked between SQL calls. Privacy export payment matching is linear; full export streaming is outside this change.
- No schema migration is introduced. Original workspace changes remain separate from this release.
