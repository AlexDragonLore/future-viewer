# REG.RU HTTPS email transport

`EMAIL_TRANSPORT=RegruWebmail` selects an authenticated adapter to REG.RU Roundcube at `https://webmail.hosting.reg.ru/`. It uses the existing paid mailbox and normal HTTPS certificate validation. The provider then sends the email using its own mail infrastructure, retaining the domain's REG.RU SPF/DKIM configuration.

This is a temporary compatibility adapter to the webmail interface, not an officially supported email API. Provider UI changes, CAPTCHA, account restrictions or rate limits can interrupt delivery. Keep SMTP as the preferred long-term transport when the hosting provider restores outbound SMTP access.

## Configuration

Keep credentials only in the root-owned, mode-600 production environment file. Set:

```dotenv
EMAIL_TRANSPORT=RegruWebmail
EMAIL_USERNAME=no-reply@alex-taro.ru
EMAIL_PASSWORD=<existing mailbox password>
EMAIL_FROM=no-reply@alex-taro.ru
```

The sender identity must match the mailbox. `EMAIL_HOST`, `EMAIL_PORT` and `EMAIL_USE_SSL` are unused by this transport; keep the verified SMTP settings there for a later switch back. Compose continues to set `Email__FrontendUrl` from `APP_DOMAIN`.

Each send creates its own authenticated cookie session. Form CSRF tokens and compose state are required. Redirects must remain on the fixed HTTPS origin. The adapter requires an explicit successful-send response and throws on authentication, form or SMTP failures. It never automatically retries a potentially accepted message after a timeout. Do not add generic HTTP retry middleware around the final send request.

## Verification and diagnosis

- Send a harmless diagnostic message using the real sender, then verify receipt in the controlled mailbox. A successful HTTP status is insufficient evidence of sending.
- Exercise registration or password reset on a controlled site test account and confirm that the email contains an `https://alex-taro.ru/` link.
- Inspect backend errors for the transport stage and failure category. Do not log passwords, cookies, CSRF tokens, auth links, message bodies or entire provider responses.
- Repeat the delivery check after email-related deployments or provider UI changes. API errors must remain visible to monitoring; do not turn transport failures into a false success.
- Verify the production containers and `https://alex-taro.ru/health` after configuration changes.

## Rollback / return to SMTP

Back up the current environment and record the current backend image before deploying. For deployment rollback, restore that image and environment and recreate only the backend. Preserve the new image for diagnosis. Returning to an environment without a configured email sender also restores that version's existing registration behavior, so do not silently disable email as a response to delivery failures.

Once outbound access to `mail.hosting.reg.ru:465` is restored, verify TLS and SMTP login, set `EMAIL_TRANSPORT=Smtp`, retain `EMAIL_HOST=mail.hosting.reg.ru`, `EMAIL_PORT=465`, `EMAIL_USE_SSL=true`, and the same mailbox credentials. Recreate the backend, verify an actual delivered message, and check health again.
