# Save reading history by default

Enable saving questions, cards, and interpretations by default for signed-in users. Keep the account switch and per-reading checkbox functional. Guest readings retain their existing temporary behavior; previously unsaved text cannot be restored.

## Implementation

- Default new accounts and omitted reading save options to enabled.
- Migrate active existing accounts from the previous implicit disabled default, preserving every explicit history-setting choice and pending account deletion.
- Match the form defaults and explanatory text to the account preference. Preserve explicit draft/checkbox opt-outs.
- Keep the existing transient processing and deletion behavior when saving is disabled.

## QA / verification

- Backend domain and Docker-backed integration tests: registration defaults, omitted save option, explicit per-reading/account opt-outs, migration preservation, guest behavior.
- Frontend type-check, tests, production build, and rendered browser checks of the checked default and opt-out behavior.
- Existing backend/frontend/release checks, plus production container status, health, migration/default verification, and browser smoke.

## Deploy

- Work in isolated branch `codex/history-default-on`, based on current `origin/main`; preserve the original dirty checkout.
- Push the tested commit and fast-forward `main` to it; build both amd64 application images from that revision with the existing public frontend configuration.
- Preserve server-owned secrets and previous images/revision; take and restore-check a private server-local database backup before startup migration.
- On `deploy@alex-taro.ru`, update `/opt/future-viewer` with `git fetch origin main` and `git merge --ff-only origin/main`.
- With the verified full SHA as `FV_IMAGE_TAG` and `FV_SHARED_EDGE_PORT=18080`, run `docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-deps --no-build --pull never backend frontend` using sudo as required.
- Verify Compose status, `https://alex-taro.ru/health`, new image references and migration, expected aggregate preferences, production browser smoke, and neighboring containers. Preserve existing Caddy and PostgreSQL containers.
