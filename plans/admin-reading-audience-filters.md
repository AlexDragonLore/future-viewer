# Admin reading audience filters

## Implementation

- Add compact All / Subscribers / Registered / Guests controls to the administrator message list. Subscribers have a currently active, unexpired subscription according to the existing admin subscription badge; Registered includes every reading with an account owner, including subscribers; Guests have no account owner.
- Add a backwards-compatible optional `audience=all|subscribers|registered|guests` parameter to the existing admin readings endpoint. Validate unknown values. Apply predicates in SQL before count and pagination, combined with existing text and owner filters. Preserve compact responses, lazy answers, authorization and the current query budget. No migration.
- Include audience in frontend request/cache keys; switching resets to page one and rejects stale responses. Preserve search, refresh, account reset, current full-question layout and pagination scrolling.
- Implement only in the clean release worktree at `c8e7de3`; preserve unrelated changes in the original workspace.

## QA / verification

- Backend integration tests: active/expired/free/null-expiry subscriptions, registered/guest counts, combinations with search and owner, ordering/pagination, invalid values, authorization and bounded query count.
- Frontend unit tests: parameter serialization, audience switching with search, page/cache isolation, response races and account reset. Run `npm run type-check`, `npm test`, `npm run build`; full backend `dotnet test backend/FutureViewer.slnx` once edits settle.
- Rendered QA using Browser Use/in-app browser if available; if no enabled browser is exposed, use Playwright and inspect screenshots at desktop, 390 and 320 px. Check accessible active state, filters, search, empty/error states and pagination without horizontal overflow.
- Production browser smoke uses public pages plus published admin assets with all API calls intercepted using synthetic fixtures. Do not create real accounts, readings or payments; distinguish UI smoke from real authenticated backend verification.

## Deploy

- Target branch: `main`. Fetch and confirm current baseline, commit the verified scope, push with `git push origin HEAD:main`. Build both images from that exact source SHA through the established temporary GitHub Actions delivery branch.
- Production checkout `/opt/future-viewer` on `deploy@80.87.104.245`; protected environment `/opt/fv-app/.env.production`. Current backend/frontend both source `c8e7de36d18ea1e2f277450af900d0e298dbd3ee`. Verify separate current image IDs, archive/config digests, encrypted backup with isolated restore and unchanged migrations before replacement.
- Fast-forward checkout to the fixed release and update only its image tag. Run `sudo -n env FV_IMAGE_TAG=<sha> FV_SHARED_EDGE_PORT=18080 FV_LEGACY_EDGE_IP=172.18.0.5 docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-deps --no-build --pull never backend frontend`.
- Verify `sudo -n env FV_SHARED_EDGE_PORT=18080 docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps`, `GET https://alex-taro.ru/health`, exact application images, protected endpoints, deployed filter assets and production browser smoke. Preserve Caddy, PostgreSQL, persistent resources and neighboring services. Roll back application images/config on failed verification; do not restore over the live database.
- Remove the temporary delivery branch and archive its worktree after success. Record the release and checks in the original workspace plan.

## Verification results

- Full backend solution: 467 passed (10 domain, 195 services, 262 integration), no failures/skips; includes 18 targeted admin integration tests. Filters preserve the existing three SQL-query budget for registered/subscriber pages, with two for an empty guest page. Expiry boundary and list/count/badge share one UTC instant.
- Frontend: 374 unit tests across 45 files, type-check and production build passed. Ten rendered admin scenarios passed, including filter/search/pagination, late responses, keyboard controls and 320 px wrapping. Browser Use returned no enabled browsers; repository Playwright was used instead. Screenshots inspected under `/tmp/fv-admin-audience/`.
- Subscription grant/revocation while filtering subscribers resets and reloads the filtered page, preventing stale membership/counts; reload errors leave no stale rows. Independent review approved the final implementation.
- Technical compliance and `git diff --check` passed. Existing package advisories/documentary metadata warnings remain outside this change. The previous main CI had two existing guest-limit E2E failures; those unrelated scenarios were not changed.
