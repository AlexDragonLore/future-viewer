# Branded transactional emails

## Scope and implementation

- Replace the plain registration and password-reset bodies with a shared, responsive HTML email template in the existing isolated checkout. Use the site's violet palette, a clear action button, link expiry, fallback URL and the signature «Магистр · Вуаль Грядущего».
- Use inline styles and presentation tables with no external images, scripts or fonts so the message remains useful when email clients block remote content. Encode links and keep the existing authentication/token behavior.
- Apply the template to registration, resend verification and password reset. Do not add marketing mail or change the sender avatar.

## QA / verification

- Exercise the real renderer using demo links; inspect both email variants in the in-app browser at desktop and narrow mobile widths, including long-link wrapping. Save screenshots. If viewport resizing is unavailable, use fixed-width browser preview frames and inspect their rendered dimensions.
- Add focused tests for correct flow/expiry and safe link encoding; verify AuthService passes the appropriate template to the sender. Run the backend solution tests and a Release build. Frontend code is unchanged, so npm tests/build are not needed.
- After deployment, trigger the password-reset flow for the existing controlled no-reply mailbox; verify the received HTML and correct link target without consuming the token.
- Send a clearly labeled design sample to the user's authorized address, duntsev010@mail.ru, using the production transport and safe demo links. Check the sent message in webmail; the user will confirm its appearance in their Mail.ru app.

## Deploy

- Commit and push `codex/branded-auth-emails`, then fast-forward `main`. Use `[skip ci]` after manual checks: the normal main workflow rebuilds unrelated services and prunes images, while this update only needs the backend.
- Build the tested backend image for linux/amd64, tag it with the commit SHA, transfer it and verify its checksum before loading. Retain the previous backend image for rollback; no environment changes are needed.
- On `/opt/future-viewer`, fetch and fast-forward main, preserving existing unrelated Caddy changes. Recreate only backend with the explicit image tag using `sudo env FV_IMAGE_TAG=<sha> docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -p future-viewer up -d --no-deps --no-build --pull never --force-recreate backend`.
- Verify `docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps`, `GET https://alex-taro.ru/health`, sanitized logs, production browser smoke and received email. If health or mail fails, restore the preceding backend image.
