# Compact admin message cards

## Implementation

- Publish only the reviewed CSS changes in AdminReadingsView and AdminReadingMessage from production baseline ace91f42c617a932391a1c8ce28a068fd7d78709.
- Reduce card padding, gaps and text size; allow metadata dates to wrap naturally. Preserve complete question/answer text, collapsed answers, 44 px controls and keyboard focus.
- Use an isolated release checkout; preserve unrelated changes in the primary checkout.

## QA / verification

- Run frontend type-check, unit tests and build against the exact release tree.
- Render the release build in the in-app browser with safe local admin fixtures at narrow/mobile and desktop widths; check full text, metadata, answer expansion and user drawer.
- Inspect the production-delivered CSS after deployment and verify the live public site in the in-app browser. If an authenticated production session is unavailable, explicitly report that limitation and verify the exact production assets with local fixture rendering instead.
- Verify Compose ps, https://alex-taro.ru/health, frontend logs and janetka.ru; compare before/after container identities to confirm only future-viewer frontend changed.

## Deploy

- Branch: codex/compact-admin-cards from origin/main; target origin/main. Commit only the two CSS files and this plan; push the branch, then fast-forward main without forcing.
- Preserve actual production public frontend build variables via an allowlisted export of VITE build arguments. Keep all runtime secrets on the server.
- Build and load a Linux amd64 frontend image tagged with the new commit SHA. Save previous revision, frontend image and container inventory for recovery.
- Server: deploy@80.87.104.245, checkout /opt/future-viewer, environment /opt/fv-app/.env.production, Compose project future-viewer.
- Server update: git fetch origin main; git merge --ff-only <release-sha>.
- Frontend update: sudo -n env FV_SHARED_EDGE_PORT=18080 FV_IMAGE_TAG=<release-sha> docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-build --no-deps frontend.
- Do not invoke the broader deployment workflow or rebuild/restart the backend, database, shared edge or neighboring projects for this CSS change.
- Post-deploy: required Compose ps and health checks, exact public CSS verification, production browser smoke and neighboring container identity/status checks. Roll back the frontend alone to ace91f42c617a932391a1c8ce28a068fd7d78709 if verification fails.

## Release verification results

- Exact isolated release: npm ci, npm run type-check, 304 frontend tests, npm run build and git diff --check passed.
- In-app browser rendered the production build with safe local fixtures: guest cards at 393 px are 185.9 px and 226.5 px high; all question text remains visible and summary controls are 44 px. Wide layout has no overflowing or clipped questions.
