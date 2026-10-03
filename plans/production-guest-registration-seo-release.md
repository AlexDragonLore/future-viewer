# Production guest registration and SEO release

## Scope

Publish the prepared guest single-card preview, simplified registration, complete Telegram removal, operational registration/payment readiness, SEO and consent-aware lead analytics to alex-taro.ru. Preserve existing credentials, domain email, subscriptions, profiles and the shared janetka.ru edge. Do not invent a Metrika counter ID or publish local operator notes.

## Implementation

1. Create an isolated release branch from current origin/main and transfer the cohesive application changes, required configs, tests and deployment files. Preserve the two production email commits and exclude local memory, secrets and unrelated personal notes.
2. Repair pending privacy migration so existing profile fields and valid email links survive. Verify legacy checkout compatibility using provider-authenticated data, recorded order ownership/amount and idempotency.
3. Keep the current public Caddy and existing Docker network/TLS volumes via a dedicated legacy-edge override; preserve janetka routing. Trust only the verified immediate Caddy address in the backend. Compact the initial cookie prompt for mobile entry.
4. Build amd64 production artifacts with actual public environment settings. Keep Metrika inactive until a real counter ID is supplied.
5. Production browser QA found that horizontal overflow rules created a separate body scroll container. Repair the scrolling root so the guest continuation link opens the registration form in the viewport; verify both desktop and mobile before a small follow-up deployment.

## QA / verification

- Frontend: type-check, unit tests, production build; relevant browser scenarios after changes, plus already completed full 26 browser cases.
- Backend: dotnet test backend/FutureViewer.slnx, including PostgreSQL integration tests and focused migration/legacy-payment regression coverage.
- Validate Compose, Caddy, runtime configuration and release technical checks. Restore a production pg_dump into an isolated disposable database and verify integrity before application migrations.
- Production rendered browser QA via in-app browser: mobile and desktop homepage, one guest card, half interpretation, reload continuation and single unchecked registration checkbox; no account creation, email send or paid transaction. Verify SEO and retired Telegram routes through HTTP.
- Full real payment settlement cannot be asserted without a paid transaction; verify configured provider readiness, rejection of spoofed source headers and existing automated verified-webhook coverage.
- After the scroll repair: focused guest browser scenarios must assert the registration heading is in the viewport after following a link from a scrolled result. Repeat the same production transition after publishing the frontend fix.

## Deploy

- Branch: codex/production-guest-seo based on origin/main; target main. Commit the reviewed release, push the release branch and fast-forward main. Avoid concurrent automatic deployment of a different topology during this manual release.
- Server: deploy@80.87.104.245; checkout /opt/future-viewer; existing env /opt/fv-app/.env.production; Compose project future-viewer.
- Before update: securely back up env, actual Caddy config, exact image references, Git revision and PostgreSQL custom-format dump. Validate restore in a disposable database. Record existing unrelated service/container identities.
- Upload/load SHA-tagged amd64 backend/frontend images, update checkout to exact main SHA, install compatible legacy-edge config and narrowly add operational/proxy settings while preserving credentials.
- Update command: FV_SHARED_EDGE_PORT=18080 FV_IMAGE_TAG=<sha> docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-build backend frontend caddy. Keep PostgreSQL and other projects running; stage backend/frontend first and edge reload after readiness when practical.
- Post-deploy required checks: docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps; GET https://alex-taro.ru/health; production browser smoke and janetka.ru baseline check.
- Recovery: prefer forward repair. Telegram schema removal makes old image-only rollback unsafe. Restore database plus exact previous images/config only in a controlled outage after accounting for writes since cutover; backup remains private on server.
