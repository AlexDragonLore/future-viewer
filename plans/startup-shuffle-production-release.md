# Publish the shuffling-card startup screen

## Scope

- Release only the startup screen in `frontend/index.html`, its standalone controller in `frontend/public/startup.js`, first-route readiness in `frontend/src/main.ts`, and their browser/SEO regression tests.
- Preserve all unrelated uncommitted work. Build the release from a clean archive of the committed revision.
- Show the CSS/SVG deck before application JavaScript, then fade into the mounted first route. Include reduced-motion behavior and a retry action for failed or stalled loading. Keep the controller external to comply with the production `script-src 'self'` policy without weakening it.

## QA / verification

- Run `npm run type-check`, `npm test`, and `npm run build` against the isolated release source, plus startup Playwright checks on desktop and mobile with the production content security policy, including a failed controller download. Backend tests are not needed for this frontend-only release.
- Check frontend/release technical compliance with the repository checker; distinguish existing documentary warnings from new failures.
- Rendered in-app browser QA already covered desktop, 390px and 320px widths, as well as a direct SEO-page entry. Repeat a production browser smoke after deployment: visible shuffled deck, successful dismissal, usable first page, and client navigation without another startup screen.
- Verify the shipped HTML/JavaScript and exact running image, Compose container status, and `GET https://alex-taro.ru/health`.

## Deploy

- Branch/merge target: `main`, remote `git@github.com:AlexDragonLore/future-viewer.git`. Use an isolated worktree based on current `origin/main`, stage only the files in scope and this plan, commit, then `git push origin HEAD:main` after release QA passes.
- Host: `deploy@80.87.104.245`; checkout `/opt/future-viewer`; existing environment `/opt/fv-app/.env.production`.
- Record the previous Git revision, frontend image/container, other container identities, and environment/Caddy checksums in a protected server-side release directory. Keep the old image for rollback.
- Export only allowlisted public frontend build arguments from effective production Compose configuration. Build `future-viewer-frontend:<release-sha>` with Docker target `prod`, platform `linux/amd64`, transfer it and verify its checksum before loading.
- Reconfirm the Caddy IP and current checkout, then `git fetch origin main` and `git merge --ff-only <release-sha>`.
- Update only the frontend: `sudo -n env FV_IMAGE_TAG=<release-sha> FV_SHARED_EDGE_PORT=18080 FV_LEGACY_EDGE_IP=<verified-caddy-ip> docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-deps --no-build --pull never frontend`.
- Post-deploy: run `sudo -n env FV_SHARED_EDGE_PORT=18080 docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps`, `GET https://alex-taro.ru/health`, production browser smoke, and compare non-frontend container identities/configuration checksums.
- If verification fails, repeat the frontend-only update with the recorded previous image tag. Preserve existing runtime configuration, backend, proxy, neighboring site and persistent volumes.
