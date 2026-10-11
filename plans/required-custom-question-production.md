# Publish the required custom question

## Implementation

- Publish the frontend change from `69fa58e`: require the visitor's own question, remove the default question, and clarify the field prompt. Preserve continuation of existing guest readings.
- Align the generated homepage's static SEO instructions with the required question; remove the previous claim that visitors can leave it empty.
- Build the exact pushed release from a clean Git archive for Linux amd64, preserving the current production public frontend build arguments.
- Update only the frontend container; this release changes neither backend code nor the database schema.

## QA / verification

- Completed for the application change: frontend type check, all 336 unit tests, production build, independent diff review, and rendered in-app browser checks of empty input, whitespace, custom input and the 393px layout. Unit coverage includes existing and expired guest continuation.
- Run frontend and release technical compliance checks. Existing documentary warnings do not change with this release.
- After deploy, inspect the public form in the in-app browser: updated prompt, disabled empty/whitespace submission, enabled custom question; create one synthetic, non-sensitive guest reading to verify the submitted question reaches the result.
- Verify the exact deployed image, public frontend assets, required Compose status and `GET https://alex-taro.ru/health`. Compare container identities to verify neighboring services and the backend remain unchanged.

## Deploy

- Target branch: `main` on `git@github.com:AlexDragonLore/future-viewer.git`. The code is already pushed; commit this release plan and `git push origin main`, then record the exact release SHA.
- Production host: `deploy@80.87.104.245`, checkout `/opt/future-viewer`, environment `/opt/fv-app/.env.production`.
- Record previous revision, frontend image, container identities and environment/Caddy checksums in a protected server-side release directory. Retain the existing frontend image for rollback.
- Export only allowlisted public `VITE_*` build arguments from the effective production Compose configuration; leave runtime secrets on the server.
- Build `future-viewer-frontend:<release-sha>` with target `prod` and platform `linux/amd64`, then transfer and load the image.
- Update checkout with `git fetch origin main` and `git merge --ff-only <release-sha>`.
- Update command: `sudo -n env FV_IMAGE_TAG=<release-sha> FV_SHARED_EDGE_PORT=18080 FV_LEGACY_EDGE_IP=172.18.0.5 docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-deps --no-build --pull never frontend`. Reconfirm the existing Caddy address before execution.
- Post-deploy: `sudo -n env FV_SHARED_EDGE_PORT=18080 docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps`, health endpoint, image/asset verification and production browser smoke.
- Recovery: run the same frontend-only update with the recorded previous image tag if verification fails. Preserve runtime configuration and all persistent volumes.
