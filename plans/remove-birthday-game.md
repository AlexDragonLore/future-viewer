# Retire the completed private birthday game

The owner confirmed that the recipient received the gift and requested removal of
the game and closure of the current chat. Preserve the main tarot service.

## Implementation
- Remove the Vue game, its API client, house artwork component, server game/access
  services, obsolete deployment workflow, private-assets override and game docs.
- Return HTTP 410 for the retired page and every old birthday API path; preserve
  no-store/noindex headers and privacy guards for old secret URLs.
- Remove server-only birthday settings and the private postcard/certificate files.
- Do not touch Ozon redemption, the main application's data, Data Protection keys,
  PostgreSQL, public Caddy, TLS, or neighboring projects.

## QA / verification
- Validate environment cleanup with tests covering unrelated settings, comments,
  CRLF, missing final newline and case-insensitive birthday setting names.
- Run the existing backend and frontend CI workflows, including integration tests,
  frontend type-check/unit tests/build and existing browser compliance tests.
- Replace obsolete game-flow integration tests with checks that old key, ticket,
  answer and asset routes all return 410 with no response body and no-store.
- Build exact-commit amd64 images; validate Nginx configuration and confirm the
  retired page returns 410 inside the built frontend image. Confirm game chunks
  are absent from the image.
- After deploy, verify main page 200, health/database readiness, retired page/API
  410, expected application images and unchanged unrelated container identities.
- The local Mac is locked, so live UI inspection is unavailable. CI browser tests
  and the production HTTP checks cover this removal.

## Deploy
- Isolated branch from main `2322577ea269b2a07563b1a62ef373bac0869110`.
- Push the checked change to the maintenance branch; its one-time workflow waits
  for backend and frontend checks before deployment through the existing pinned
  production SSH connection.
- On `deploy@80.87.104.245`, preserve the current images/environment/revision,
  fetch the exact branch commit and reject concurrent changes to remote main or
  database migrations.
- Recreate only backend/frontend using the existing production Compose files and
  legacy-edge override; omit the retired birthday bind mount.
- Keep private assets available for rollback until the replacement passes checks,
  then delete `/opt/fv-app/birthday` and `/tmp/fv-birthday-d94a73c6`. Remove retired
  birthday settings from feature-specific environment snapshots while preserving
  all unrelated rollback credentials.
- Verify required production Compose ps and `https://alex-taro.ru/health`.
- Fast-forward remote main to the deployed commit only after successful production
  verification, checking that its previous head still matches the reviewed base.
- Delete the earlier key-rotation branch and local private key material; archive
  the current chat after reporting the verified result.
