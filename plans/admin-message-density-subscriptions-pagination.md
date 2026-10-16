# Compact admin messages, purchased subscriptions and faster pagination

## Scope

- Make administrator question cards substantially shorter while keeping full questions and answers accessible.
- Show the actual latest paid subscription purchase (tariff, duration, amount and payment date) beside the user, with current access status/expiry shown separately. Include this information in the users list/detail as well as message rows. Do not infer a purchase from a manual access grant or from days remaining.
- Reduce pagination payload and rendering work by omitting interpretation bodies from compact list requests and loading/rendering an answer only when opened. Preserve the existing full-list response for older clients during rollout.
- Remove avoidable per-user database queries, use bounded batch lookups/projections, and keep navigation responsive without stale page responses or exposing cached admin data across account changes.
- Implement from current `origin/main` in an isolated worktree; preserve unrelated local changes. Retain the published 30-day guest / indefinite registered history policy. No payment-processing or feedback-removal changes are included.

## QA / verification

- Backend: run `dotnet test backend/FutureViewer.slnx` with Docker; cover compact/full response compatibility, admin authorization, lazy answer retention/404 behavior, latest paid versus pending/cancelled purchases, expired/manual access, and bounded database query counts.
- Frontend: run `npm run type-check`, `npm test`, `npm run build`; verify collapsed/full question behavior, loading answers only on demand, purchase labels, pagination races/errors and account reset.
- Render the administrator messages/users screens with realistic synthetic fixtures in the in-app browser; inspect desktop and mobile card density, readable subscription information, expanding a long question/answer and pagination. Use Playwright for exact viewport/latency assertions as needed. Avoid mutating a real production account.
- Compare representative list payload/render behavior and query counts before/after; use aggregate-only production SQL if necessary, without exporting question/answer text or identities.
- Run technical compliance checks and review the final release diff.

## Deploy

- Target branch: `main`, remote `git@github.com:AlexDragonLore/future-viewer.git`. Commit only this tested admin change and the plan, reconcile concurrent remote updates, then `git push origin HEAD:main`.
- Production: `deploy@80.87.104.245`, checkout `/opt/future-viewer`, protected env `/opt/fv-app/.env.production`, Compose project `future-viewer`.
- Use the existing deployment identity through a temporary GitHub Actions delivery branch if direct SSH remains unavailable. Pin application source to the tested release SHA; filter public frontend build arguments on the server before transfer. Verify image archive checksums and exact configuration digests, accounting for server-local Docker IDs.
- Record the current Git revision, separate application images, configuration and persistent resources. Verify an encrypted database backup by restoring it into an isolated container before backend replacement. No new schema migration is expected; reconcile the migration list explicitly.
- Update checkout with `git fetch origin main` and `git merge --ff-only <release-sha>`. Change only `FV_IMAGE_TAG` in the existing runtime environment.
- Server update: `sudo -n env FV_IMAGE_TAG=<release-sha> FV_SHARED_EDGE_PORT=18080 FV_LEGACY_EDGE_IP=<verified-caddy-ip> docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-deps --no-build --pull never backend frontend`.
- Post-deploy: run `sudo -n env FV_SHARED_EDGE_PORT=18080 docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps`, `GET https://alex-taro.ru/health`, verify running image IDs, shipped admin assets, production browser smoke and unchanged neighboring services including `janetka.ru`.
- Roll back only the application images/configuration if verification fails, preserving the database, Caddy and all persistent volumes.

## After deployment: performance review

- The user requested a review of other project areas after this admin release is live. Start this review only after production verification completes.
- Inspect the published backend and frontend for repeated database/API calls, oversized responses, unnecessary loading/rendering and unbounded work. Distinguish measured query counts from potential issues and local synthetic benchmarks from production timing.
- Report concrete locations, user-visible impact and prioritized recommendations. Do not mix unrelated fixes into this admin deployment.

## Pre-release verification result

- Backend: 445 tests passed. SQL interception of the admin list operation verifies three queries for 20 users (page projection with aggregates, count, one payment batch), replacing the former 62; compact question pages also use three. Common authentication overhead is outside this operation-level count.
- Frontend: type-check, build, 339 unit tests and six desktop/mobile admin E2E scenarios passed; technical compliance checks and independent release review passed.
- Browser Use reported no available browser, so rendered QA used the repository's Playwright fallback with synthetic admin data. Two before/after comparisons passed at 1280×900 and 390×844, including full question/answer expansion, paid tariff display, users and user detail, and no horizontal overflow.
- In that identical fixture, average question-card height changed from 200 to 113 px on desktop and 217 to 190 px on mobile. Fully visible question texts changed from 1 to 4 and from 0 to 2 respectively. Hidden answer DOM blocks dropped from 20 to zero; opening an answer made one request.
- Aggregate-only production preflight confirmed the previous release remains live. The latest 20-question page contained 1,702 question-text bytes and 69,457 answer-text bytes before JSON metadata/compression; the new compact list avoids transferring those answer bodies until requested. No private question, answer or identity was exported.

## Published admin release and requested UI refinement

- The primary admin release `9128c51c5ed9abdb5432301fa09790058b2138fd` was pushed to `main` and deployed successfully through run https://github.com/AlexDragonLore/future-viewer/actions/runs/38960403779. Backup/restore, container/image checks, health, the new protected answer endpoint, shipped purchase labels and public browser smoke passed.
- The user then requested still denser cards: remove the explanatory paragraphs and visible spread/deck/retention/no-subscription prose; use small accessible icons and a compact purchased-tariff badge, preserving full purchase details through the user drawer.
- Fix Next/Previous navigation so a successfully loaded page scrolls to its first entries with the fixed-header offset. Failed or stale requests must not scroll the wrong content; reduced-motion preferences must be respected.
- QA for this refinement: frontend type-check, unit tests, build and responsive browser tests; use at least two full pages to verify pagination scroll rather than relying on a short final page. Repeat rendered screenshot inspection and public production smoke. Backend behavior/schema are unchanged.
- Publish the frontend refinement to `main` after tests. Build and transfer only `future-viewer-frontend:<new-sha>` with preserved production build settings, verify archive content/configuration digest, snapshot the previous frontend/configuration, fast-forward the server checkout and update only the image tag.
- Frontend-only update command: `sudo -n env FV_IMAGE_TAG=<new-sha> FV_SHARED_EDGE_PORT=18080 FV_LEGACY_EDGE_IP=172.18.0.5 docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-deps --no-build --pull never frontend`. Verify the backend container/image and all other services remain unchanged, required Compose status/health, shipped assets and public browser smoke; use the saved frontend image/environment for rollback if needed.

## Refinement verification

- Frontend type-check/build, all 344 unit tests and six desktop/mobile admin E2E scenarios passed. Pagination uses 41 fixture rows so the second page remains full; both Next and cached Previous place its first row 75–90 px below the viewport top. Failed and superseded requests do not scroll.
- Rendered Playwright checks passed at 1280×900, 390×844 and 320×740. Mean card heights are approximately 62/94/95 px. The same fixture shows eight fully visible questions on desktop and four at 390 px. At 320 px the email remains clickable and no horizontal overflow occurs. Purchase and email controls open the full user drawer.

## Performance review after the primary production release

The following findings are from the published `9128c51` source; production request timings were not measured. These areas were not changed by the later admin icon/scroll refinement.

1. **Completed result replay and repeated Markdown work:** `frontend/src/views/ResultView.vue:52,103,126` treats nonempty streaming text as an active stream even when complete. `stores/useReadingStore.ts:118` restores an already complete guest result into that text, so it animates again. Each display update reparses/sanitizes the accumulated Markdown. Show restored completed responses immediately and process genuine streaming updates in bounded batches; preserve safe Markdown sanitization.
2. **History overfetch:** `ReadingRepository.GetHistoryAsync` and `ReadingService.GetHistoryAsync` return 50 full interpretations and all card data, although `frontend/src/views/HistoryView.vue` renders brief previews. Fifty ten-card readings mean up to 500 card records, and the joined database result repeats interpretation data. Use a paginated summary projection and the existing full-detail endpoint.
3. **Duplicate subscription refresh:** `SiteHeader.vue`, `HomeView.vue` and `ProfileView.vue` independently call `useAuthStore.refreshSubscription`. Initial authenticated Home/Profile loading can make two identical requests. Deduplicate in-flight requests per session; retain mandatory refresh after purchases/readings and session-reset protection.
4. **Unused personalization queries:** `PersonalizationService.GetPromptContextAsync` loads the user and memory rules for authenticated readings, but both methods in `OpenAIInterpreter` explicitly discard that context to preserve the privacy boundary. Skip these two reads while the context is intentionally disabled; do not re-enable transmitting profile/memory data as a performance fix.
5. **Achievement query duplication:** the normal existing-reader path through `/api/achievements/me` performs nine sequential SELECTs including the common JWT check, before any new grants. Catalog and owned-achievement reads are repeated, and all scored-feedback entities are loaded for aggregates. Reuse the catalog/result within one operation and aggregate scores in SQL; preserve grant uniqueness under concurrent requests.
6. **Route-transition delay:** `App.vue` combines `mode="out-in"` with a 0.5-second leave transition in the normal motion mode. The next component's mount/API work waits for that transition. Shorten or overlap it while retaining reduced-motion behavior.
7. **Initial SEO catalog payload:** router/SEO imports bring `tarotSeoCatalog.js` into the initial bundle. The catalog source is about 54 KB; this is not a claim of equivalent gzip savings. Separate the small route metadata from detailed catalog/JSON-LD content and load the latter only where needed.
8. **Lower-priority scale issues:** privacy export materializes all collections and compares legacy payments against orders with `O(P×O)` membership checks; a `HashSet` is a narrow improvement before considering streaming exports. Cleanup deletes all expired rows in one transaction, which may matter after a large backlog; batch if measured scale warrants it. The guest `(UserId, CreatedAt)` index already exists.

Prioritize completed-result rendering, history summaries and duplicate subscription requests for user-visible improvement. Do not replace required JWT revocation checks or quota locks with stale caching, or parallelize operations on a shared EF Core context.
