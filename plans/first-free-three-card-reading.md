# First free three-card reading

## Behavior

- A newcomer can start a free three-card reading. Anonymous visitors keep the existing half-interpretation preview and account verification flow to unlock the complete reading.
- An account with no previous readings can choose its introductory three-card reading. Once a reading exists (including a claimed guest reading), only one single-card reading per UTC day is free.
- The introductory reading consumes that day's free allowance. Removing it from history does not restore the introductory entitlement.
- Active paid access continues to allow all spreads. Celtic Cross always requires paid access.

## Implementation

- Expose introductory eligibility in subscription status and enforce spread restrictions in the backend.
- Keep introductory use and last-reading date on the account so physical deletion cannot reset free allowances; backfill from retained existing readings.
- Serialize account reading creation and guest claims with a short transaction and owner lock; release before AI interpretation.
- Start the guest journey with three cards, preserve continuation tickets, and prevent a claimed guest reading from granting another introduction.
- Default newcomers to three cards and show the introductory/daily rules in the frontend and generated homepage metadata.

## QA / verification

- Run backend unit and integration tests for introduction eligibility, daily limits, guest creation/claim, and removal from history.
- Run frontend type checking, unit tests, production build, and relevant Playwright scenarios.
- Inspect rendered guest and authenticated states in the in-app browser. Use API fixtures for authenticated states and desktop/mobile Playwright checks for exact viewport coverage.
- Review the final diff for consistency of API/UI eligibility and stale one-card guest copy.

## Delivery

The user requested push and production verification after reviewing the local implementation.

## Deploy

- Branch/merge target: `main` in `git@github.com:AlexDragonLore/future-viewer.git`. Commit only this feature and its QA/documentation; preserve parallel unrelated working-tree edits.
- Push: `git push origin main`, record the exact commit SHA, and build backend/frontend images from an isolated checkout of that SHA with the existing public frontend build values.
- Production host: `deploy@80.87.104.245`; checkout `/opt/future-viewer`; secrets remain in `/opt/fv-app/.env.production` with mode 0600.
- Save previous Git revision, backend/frontend image references, env and Caddy configuration in a protected server-side release directory. Make a database backup and verify restoring it in an isolated PostgreSQL container before the new migration runs.
- Server update: `cd /opt/future-viewer && git fetch origin main && git merge --ff-only <pushed-SHA>`. Validate the existing runtime env and verify current Caddy address ownership and persistent mounts.
- Load images tagged with the pushed SHA, then run with `FV_IMAGE_TAG=<pushed-SHA> FV_SHARED_EDGE_PORT=18080`: `docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-deps --no-build --pull never backend frontend`. Preserve the existing Caddy container because its configuration is unchanged.
- Post-deploy verification: verify the same Compose project reports the new image SHA; check `https://alex-taro.ru/health`, migration history and safe startup diagnostics, retained PostgreSQL/Data Protection mounts, and continued health of Janetka and other neighboring services.
- Production browser smoke: inspect the new three-card entry and open one non-sensitive test guest reading. Verify three cards, partial interpretation and registration continuation. Verify subscription status/first-free/daily enforcement using a dedicated smoke account and production API where safely available; do not alter a real customer's quota or create a paid checkout.

## Verification results — 2026-10-08

- Frontend: `npm run type-check`, `npm test` (319 tests), and `npm run build` passed.
- Playwright: `guest-reading.e2e.ts` and `home-paid-access.e2e.ts` passed all 20 desktop/mobile scenarios, including three-card preview, continuation after verification, intro default selection, established-account denial, and daily quota.
- In-app browser: verified rendered three-card guest result, preview after an established account's denied claim, return to single-card daily access, successful newcomer claim, consumed daily quota, and default three-card selection for an unused account.
- In-app browser used a local static build with mocked APIs. The existing static-preview guard hides payment offers; mocked desktop/mobile Playwright scenarios cover payment UI. Payment-success continuation routing has focused component tests.
- Backend domain-services: 170 tests passed. Focused Docker-backed integration coverage: 44 tests passed for subscription, guest claims, concurrency, erasure, export, and migration upgrade.
- Final full backend solution: `dotnet test backend/FutureViewer.slnx` passed 422 tests (Domain 10, DomainServices 170, Docker-backed Integration 242), with none skipped.
- Final diff review and `git diff --check` passed. No commit, push, or production deployment was performed.
