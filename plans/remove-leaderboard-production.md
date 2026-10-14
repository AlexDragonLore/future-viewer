# Remove leaderboard from production

## Implementation

- Release the leaderboard removal from an isolated checkout based on `486b722`: remove its public page/navigation, profile ranking and backend API; keep achievements and feedback scoring.
- Redirect `/leaderboard` to `/`. Remove obsolete feature code, update related tests and current documentation.
- Exclude the parallel, unfinished payment and reading-limit changes from this release. No database migration or runtime setting change is required.

## QA / verification

- Run frontend type-check, all unit tests and production build, plus backend `dotnet test backend/FutureViewer.slnx` on the isolated release.
- Run related profile/achievement Playwright checks at mobile and desktop widths; use the in-app browser for rendered verification of the old URL redirect and navigation.
- Run backend/frontend/release technical compliance checks, `git diff --check`, and validate the production nginx configuration.
- Compare production migration history with this unchanged migration set before restarting the backend. Record image references and preserve rollback state.
- After deployment, check production desktop/mobile rendering, absence of leaderboard links, redirect, retired API 404s, and achievements availability. If no authenticated production session is available, cover achieved/locked achievement rendering with local fixture E2E and verify the production authentication guard without creating an account.

## Deploy

- Release branch: `codex/remove-leaderboard-release`; merge target: `main`. Commit the reviewed isolated release, push the release branch and fast-forward remote `main` only after checking the current remote head. Do not change the dirty original checkout.
- Server: `deploy@80.87.104.245`, checkout `/opt/future-viewer`; env `/opt/fv-app/.env.production`; Compose project `future-viewer`.
- Preserve previous revision, backend/frontend images, private env and edge config under `/opt/fv-app/deployments`. Reconfirm the Caddy address and persistent mounts; leave the shared edge and database running.
- Build both images for `linux/amd64` from the exact release SHA, using the existing allowlisted public frontend build arguments. Transfer and load them. Fetch origin and fast-forward the server checkout to the release SHA, preserving its existing Caddy modification.
- Update command: `sudo -n env FV_IMAGE_TAG=<release-sha> FV_SHARED_EDGE_PORT=18080 FV_LEGACY_EDGE_IP=<verified-current-address> docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-deps --no-build --pull never backend frontend`.
- Required post-deploy checks: `docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps`, `GET https://alex-taro.ru/health`, image verification, production browser smoke and neighbor container/health checks.
- If verification fails, recreate backend/frontend with their individually recorded previous image references. With the migration set unchanged, rollback does not require database restoration.

## Pre-deploy results

- Isolated release: frontend type-check/build, 328 unit tests, 46 profile/tariff browser E2E scenarios, and all 440 backend tests passed. Achievements retain earned and locked cards and navigation at 320px and 1280px.
- Fixed two stale test-only profile checks: current free-mode copy and deterministic scrolling before the unchanged full-visibility assertion. Application payment behavior is unchanged.
- In-app browser confirmed the old URL redirects to the rendered homepage without a leaderboard link. Backend/frontend/release technical compliance checks and `git diff --check` passed; existing documentary metadata warnings remain.
- Production migration history exactly matches the unchanged 20-migration release set. Caddy remains at `172.18.0.5`; database and Data Protection mounts will be preserved.
