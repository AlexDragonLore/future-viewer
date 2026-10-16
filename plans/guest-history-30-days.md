# Publish 30-day guest history retention

## Scope

- Retain guest questions, answers and cards for 30 days, with matching administrator visibility and expiry labels.
- Preserve indefinite registered saved history and explicit privacy/account deletion.
- Keep guest continuation tickets and claiming limited to 24 hours. Exclude guests from the separate 48-hour cleanup of legacy unsaved account records.
- Release only these changes from an isolated checkout of current `origin/main`; leave unrelated payment, feedback and usage-counter work in the original checkout untouched. No database migration is introduced.

## QA / verification

- Run `dotnet test backend/FutureViewer.slnx` with Docker, including guest-retention boundaries, both cleanup paths, administrator queries, saved account history and 24-hour guest access.
- Run frontend `npm run type-check`, `npm test`, `npm run build`, and backend/frontend/release technical compliance checks.
- Inspect the rendered administrator screen with local synthetic fixtures using the in-app browser; confirm the guest label and creation-plus-30-days expiry. Production administrator authentication is unavailable, so validate that protected behavior with integration/component tests and a local rendered fixture.
- After publishing, inspect the production homepage and navigation with the in-app browser without creating real accounts, payments or readings.

## Deploy

- Branch/merge target: `main` at `git@github.com:AlexDragonLore/future-viewer.git`. Commit the isolated tested patch, fetch and reconcile any concurrent `origin/main` update, then `git push origin HEAD:main`.
- Host: `deploy@80.87.104.245` (`alex-taro.ru`); checkout `/opt/future-viewer`; protected env `/opt/fv-app/.env.production`; Compose project `future-viewer`.
- Record previous checkout SHA, individual application images/container IDs, protected env/Caddy backups, migration list, network address and volume mounts. Compare migration history against the release before any restart. Prepare a protected database backup and verify an isolated restore before replacing the backend; no schema changes are expected in this release.
- Export only allowlisted public frontend build arguments from production configuration. Build backend and frontend `linux/amd64` images tagged with the tested commit SHA, transfer archives, verify checksums, and load them.
- Update checkout using `git fetch origin main` and `git merge --ff-only <release-sha>` while preserving any known local Caddy configuration. Update only `FV_IMAGE_TAG` in the existing protected environment.
- Update command: `sudo -n env FV_IMAGE_TAG=<release-sha> FV_SHARED_EDGE_PORT=18080 FV_LEGACY_EDGE_IP=<verified-caddy-ip> docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-deps --no-build --pull never backend frontend`.
- Preserve PostgreSQL, Caddy, Data Protection/TLS volumes, networks and neighboring services.
- Verify `sudo -n env FV_SHARED_EDGE_PORT=18080 docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps`, `GET https://alex-taro.ru/health`, exact running image IDs, unchanged migration history, startup diagnostics, production browser smoke, and `https://janetka.ru` health.
- Roll back application images and the saved environment if verification fails, using the original separate backend/frontend image references. Do not restore a database or remove volumes for this schema-preserving release.

## Pre-release verification

- Full backend suite: 441 passed (10 Domain, 191 DomainServices, 240 integration), no failed or skipped tests.
- Frontend: type-check, all 329 tests, production build and technical compliance checks passed. Existing package advisory/documentary warnings remain outside this release.
- Independent diff review found no blocking issues or unrelated feature changes.
- Rendered in-app browser QA of the isolated release showed the 30-day guest label, the correct deadline on a ten-day-old synthetic guest record, the indefinite account-history description and the saved answer.
