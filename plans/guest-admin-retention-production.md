# Publish anonymous admin messages with 24-hour retention

## Scope and implementation

- Start from production revision `8c08abd60f736aea61373a7be683ecffe554c7b4` on `origin/main` in an isolated worktree.
- Transfer only the guest content persistence, admin listing/expiry UI, ticket/claim deadline, minutely cleanup job and regression tests already implemented locally. Preserve newer production AI prompts and PostgreSQL timestamp assertions. No schema migration is required.
- Older anonymous rows have no stored question or interpretation; do not invent or backfill their contents.

## QA / verification

- Run `dotnet test backend/FutureViewer.slnx`, frontend `npm run type-check`, `npm test`, `npm run build`, and technical compliance checks for backend/frontend/release.
- Render the anonymous admin row and expiry in the in-app browser with local fixtures at desktop and mobile widths. PostgreSQL integration tests cover authorization, full guest content, expiry, cleanup/cascade deletion and claiming.
- Build both Linux amd64 images from the reviewed release tree, preserving the current public frontend configuration.
- After deployment, create one harmless guest reading in the production browser and verify its anonymous DB record and 24-hour expiry without reading other users' text. Smoke-test the production frontend. If no authenticated admin browser session is available, cover admin rendering with local browser QA and inspect the deployed admin query using the exact synthetic reading ID.

## Deploy

- Branch: `codex/guest-admin-retention`; merge target `main`. Commit the narrow change, push the release branch, then fast-forward `origin/main` without force. Avoid launching the incompatible automatic production workflow during this manual release.
- Server: `deploy@80.87.104.245`; checkout `/opt/future-viewer`; unchanged env `/opt/fv-app/.env.production`; Compose project `future-viewer`.
- Save prior checkout revision and exact backend/frontend image references in a private deployment directory. Keep the existing schema, secrets, database/Data Protection volumes, Caddy and other projects.
- Upload/load new SHA-tagged amd64 images and fast-forward the server checkout to that exact SHA.
- Update only backend and frontend using `FV_SHARED_EDGE_PORT=18080 FV_IMAGE_TAG=<sha> docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-build --no-deps backend frontend`.
- Verify `docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps`, `GET https://alex-taro.ru/health`, production browser smoke, frontend asset version, the synthetic guest record, and the unchanged neighboring site `janetka.ru`.
- Recovery: prior images and Git revision remain available; revert application images if needed. No schema changes or volume removal are part of this release.
