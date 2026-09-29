# Restore domain email through REG.RU HTTPS webmail

## Problem and scope

The production VPS and the existing DigitalOcean VPN server cannot reach REG.RU SMTP ports. The paid mailbox `no-reply@alex-taro.ru` works through REG.RU's HTTPS Roundcube webmail. Add an explicitly selected, narrowly scoped HTTPS transport using that mailbox. This is a temporary compatibility adapter to a web interface, not a provider-supported API. Keep SMTP as the default so it can be restored when network access is available.

Work in an isolated checkout based on production main; do not include unrelated local changes. Keep passwords, session cookies and message bodies out of source control and logs. Preserve TLS verification, same-origin redirects and CSRF protection. Never automatically retry a send after an ambiguous response.

## Implementation

1. Add `Email:Transport=RegruWebmail` and a dedicated sender, isolated authenticated session per delivery, fixed REG.RU HTTPS endpoint, form parsing and explicit success detection.
2. Wire transport selection and production environment configuration. Preserve SMTP behavior for existing deployments.
3. Document temporary transport limitations, rollback and operational checks.
4. Use stable .NET 10 SDK/runtime images: the existing preview runtime cannot launch assemblies targeting the stable .NET 10 framework used for verification.

## QA / verification

- Verify the actual paid mailbox using a harmless test email, including received sender/domain.
- Test the HTTPS protocol against controlled HTTP fixtures: authentication/session failure, CSRF/form mismatch, SMTP rejection, unexpected redirects, ambiguous timeout and successful delivery response.
- Run backend tests and Release build; no frontend changes are planned, so frontend tests/build are not required.
- Exercise the real sender from production, then trigger the site's existing auth-email flow to the controlled mailbox. Verify receipt and correct HTTPS links through the in-app browser. Avoid publishing real account tokens in output.
- Check production container state, `GET https://alex-taro.ru/health`, and sanitized backend logs.

## Deploy

- Commit and push isolated changes on `codex/regru-https-email`; target `main` for the patch, preserving unrelated changes. The existing main workflow rebuilds every service and prunes images, so use a `[skip ci]` commit after local QA and perform the reviewed backend-only deployment manually.
- Build a versioned backend image from the tested commit. Back up the existing production environment and save the previous backend image/tag.
- Activate the verified mailbox credentials and `EMAIL_TRANSPORT=RegruWebmail` in `/opt/fv-app/.env.production`; update `/opt/future-viewer` with the reviewed patch.
- Recreate only the backend using production/prebuilt Compose files and the explicit image tag: `docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -p future-viewer up -d --no-deps --no-build --pull never --force-recreate backend`.
- Verify `docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps`, public health, actual email receipt, and logs. Roll back the backend image and env if checks fail.
