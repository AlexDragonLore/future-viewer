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
