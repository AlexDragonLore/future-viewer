# Restore the full guest reading after registration

## Behavior

- Keep the half-interpretation preview before registration.
- Registration and email verification open the complete first three-card reading without payment.
- Preserve the same full result across the original registration tab, a separate email-verification tab, and page reloads.
- State clearly that the first reading is fully open and a paid offer applies to future additional readings.
- Preserve account ownership, the introductory entitlement, and the daily single-card limit.

## Implementation

- Save a 24-hour owner-scoped reference to the unlocked reading before removing its encrypted guest ticket. Store only reading ID, owner ID, and expiry, never the question or interpretation.
- Restore the owned reading through the existing authenticated reading endpoint when the ticket has already been claimed in another tab.
- Route an already-authenticated registration tab and the email-verification tab to the retained result.
- Publish the account identity before the token change that reloads other tabs. Provide an owned-result retry after temporary network errors and a history link for older sessions whose continuation was already removed.
- Add a real backend registration/verification/unlock regression, plus frontend owner isolation, reload, and cross-tab regressions.
- Update the browser-storage disclosure to describe the necessary result reference.

## QA / verification

- Run backend guest and published-document integration tests, frontend type checking, unit tests, and production build.
- Run rendered desktop/mobile Playwright scenarios for the original registration tab and email tab, complete text, absence of the guest lock, and full-result reload.
- Inspect the result in the in-app browser with a synthetic account. Preserve the pre-registration preview and verify that full-result and future-paid-offer labels are distinct.
- Review account-switch/expiry handling and the final diff.

## Deploy

- Branch/merge target: `main` on `git@github.com:AlexDragonLore/future-viewer.git`; commit this correction and push without force.
- Build exact-SHA backend/frontend images from an isolated checkout, preserving existing public frontend build settings. Validate technical release checks.
- Production: `deploy@80.87.104.245`, checkout `/opt/future-viewer`, server secrets `/opt/fv-app/.env.production` mode 0600.
- Preserve previous revision/images/env/Caddy config and take a private encrypted database backup with an isolated restore check. No schema migration is expected for this correction.
- Update server with `git fetch origin main` and `git merge --ff-only <pushed-SHA>`. Load images and run `FV_IMAGE_TAG=<pushed-SHA> FV_SHARED_EDGE_PORT=18080 FV_LEGACY_EDGE_IP=<verified-current-IP> docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-deps --no-build --pull never backend frontend`.
- Verify exact image SHA, Compose status, `https://alex-taro.ru/health`, retained Data Protection/PostgreSQL volumes, unchanged env/Caddy config, and neighboring Janetka/bot containers.
- Production smoke: check the half preview, successful free full unlock with a disposable synthetic verified account, and rendered full-result recovery. Do not require a paid checkout or send a registration email to a real recipient.

## Verification results — 2026-10-09

- Reproduced the original registration tab remaining on the registration page after verification in a separate tab, and full-result refresh redirecting home after the ticket was cleared.
- Backend guest and published-document integration coverage passed 15 tests, including a real register/verify/full-unlock flow and exact full-text equality.
- Frontend type checking, all 333 unit tests, and the production build passed. Cross-tab tests cover both tabs, complete final text after reload, owner isolation, and the legacy history recovery view.
- All 14 rendered desktop/mobile guest scenarios passed, including both registration/verification tabs and reloads with the final sentence visible.
- Final independent review found no remaining blocking defects. Technical release checks and `git diff --check` passed.
