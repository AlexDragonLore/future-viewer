# Admin subscription purchase history

## Implementation
- Add an admin-only, paginated subscription order history backed by existing payment orders, including repeat purchases and anonymized orders after account deletion.
- Default to paid purchases; support all orders, pending, canceled, failed and refunded statuses, email/order search and a user filter. Manual access grants are not purchases.
- Add a responsive Purchases tab and a link from the user drawer to that user's history.

## QA / verification
- Integration tests with PostgreSQL: authorization, filtering, pagination, deleted users and response data boundaries.
- Frontend type check, unit tests and production build.
- Render the page in the in-app browser; exercise desktop and mobile layouts, filters, navigation, errors and pagination using Playwright with synthetic authenticated fixtures. No real payment is needed.

## Deploy / publication
- Target: `origin/main`; work from the latest remote main in an isolated worktree and push the reviewed commit with `git push origin HEAD:main`.
- The follow-up reports that the feature is absent from the production site. Deploy the checked main revision to `deploy@80.87.104.245`, checkout `/opt/future-viewer`, using the existing environment `/opt/fv-app/.env.production`.
- The generic workflow assumes a different checkout/proxy layout. Use the established production deployment procedure with the prebuilt and legacy-edge Compose overrides, preserving the current environment and edge proxy.
- Commit/push this updated plan to `main`; build both application images for `linux/amd64` from a clean archive of that exact revision. Export only existing public `VITE_*` frontend build arguments from production. Transfer images with a verified checksum.
- Record the previous Git revision/images, environment checksum and unaffected container identities in a protected server release directory. Reconfirm the current main SHA, fast-forward the server checkout and retain the previous images for rollback.
- Server update: `sudo -n env FV_IMAGE_TAG=<release-sha> FV_SHARED_EDGE_PORT=18080 FV_LEGACY_EDGE_IP=<verified-caddy-ip> docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-deps --no-build --pull never backend frontend`.
- Verify `sudo -n env FV_SHARED_EDGE_PORT=18080 docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps`, both exact running image tags, `GET https://alex-taro.ru/health`, and production browser smoke. Verify the delivered admin navigation/chunk and authenticated view if an existing browser session is available; otherwise report that authenticated production QA was unavailable and rely on the completed authenticated local browser and API integration checks.
- On failed health/smoke, restore the recorded previous application images with the same project-scoped command; preserve environment, proxy, database, volumes and neighboring services.

## Verification results
- Backend solution: 464 tests passed (10 domain, 181 services, 273 integration).
- Frontend: 361 unit tests passed; type check and production build passed.
- Browser tests: all 6 purchase history scenarios passed across desktop/mobile, including 320px width, plus 18 existing admin scenarios passed. An initial fixture interception error was corrected before the successful rerun.
- In-app browser: synthetic admin login, rendered purchase list, expanded payment details and refund filtering checked at desktop and 390px mobile width. Real production customer data and payment creation were not used.
- The initial CI backend run passed all new purchase tests but exposed an existing flaky guest-reading test: its random GUID could match the phone-number privacy filter. Its search marker now uses only letters, preserving uniqueness without changing the production filter.
