# Activate Yandex Metrika counter 113366341

## Implementation

- Set the supplied public counter ID in the production frontend build configuration, GitHub deployment variable, and existing server environment.
- Use the existing consent-aware loader and sanitized SPA page views and conversion goals. Keep personal questions, email addresses, and authentication tokens out of analytics.
- Check real SDK initialization and the production Content Security Policy. Fix only demonstrated compatibility issues.
- Update the Yandex Direct setup guide with the counter ID and the exact registration and payment goal identifiers.

## QA / verification

- Run frontend type checking, relevant analytics unit and mocked browser tests, and the production build with the supplied ID.
- Use the in-app browser to verify the rendered production site at mobile and desktop widths: necessary-only preferences send no analytics; analytics opt-in loads and initializes the real counter; ordinary SPA navigation sends sanitized page views; revocation stops analytics.
- Use mocked SDK tests for conversions, without sending fake registrations or payments to the live counter.
- If the in-app browser cannot expose a particular network state, inspect its browser logs and resource timing alongside the automated mocked SDK coverage and report the limitation.

## Deploy

- Work in the attached clean `codex/production-guest-seo` worktree, commit the reviewed changes, push the branch, and fast-forward `main` after checking the remote head.
- Build and transfer the amd64 frontend image with `VITE_YANDEX_METRICA_ID=113366341`.
- On `deploy@80.87.104.245`, update `/opt/future-viewer` from `main`, preserve the existing private environment, and update its public counter ID and image tag. Recreate only `frontend` using `docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-build --pull never --no-deps frontend`.
- Preserve the existing Caddy edge, proxy address, TLS volumes, database, backend, and Janetka routing. If an exact CSP adjustment proves necessary, validate and reload the existing Caddy configuration with the unrelated route retained.
- Verify `docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps`, `GET https://alex-taro.ru/health`, a production browser smoke check, and `https://janetka.ru`.
- Keep the previous frontend image and a protected environment backup for a frontend-only rollback.
