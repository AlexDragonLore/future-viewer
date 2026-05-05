# Server Deployment Mode

## Summary
Add a production deployment path where a clean server can run the app with Docker after cloning the repo and executing `bash init.sh`. Keep local development separate from production, and store real credentials only on the server in a non-committed env file.

Recommended server flow after package install:

```bash
mkdir -p /opt/fv-app
git clone <repo-url> /opt/fv-app/current
cd /opt/fv-app/current
bash init.sh
```

## Key Changes
- Keep `docker-compose.yml` as the local/dev compose, but make its intent explicit in README.
- Add `docker-compose.prod.yml` for server:
  - `postgres` internal only, persistent named volume.
  - `backend` with `ASPNETCORE_ENVIRONMENT=Production`, no public port, startup migrations unchanged.
  - `frontend` built with Docker `prod` target.
  - Add a reverse proxy container, preferably Caddy, exposing `80`/`443`, serving HTTPS for the configured domain and proxying:
    - `/api/*` and `/health` to backend.
    - everything else to frontend.
- Update frontend API config so production can use same-origin API by default instead of hardcoding a public API host.
- Add production env templates:
  - `.env.production.example` committed with placeholders and comments.
  - `.env.production` ignored by git.
- Extend `.gitignore` to ignore real env files while allowing example files.

## `init.sh`
- Add root-level `init.sh`, executable, idempotent.
- Detect compose command automatically: prefer `docker compose`, fallback to `docker-compose`.
- Create `/opt/fv-app` if missing and use it as the runtime directory for server-owned files.
- If `/opt/fv-app/.env.production` does not exist:
  - Prompt for required values: domain, OpenAI key, admin email, support email.
  - Generate strong `JWT_SECRET`, Postgres password, and any other required random secrets.
  - Prompt optional Telegram, SMTP, and YooKassa values; allow empty values where the app already no-ops.
  - Save with permissions `600`.
- Validate required production values before startup:
  - domain is present for HTTPS mode.
  - `OPENAI_API_KEY` is present.
  - `JWT_SECRET` is not the dev default and is long enough.
  - DB password is present.
- Run:
  - `docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml pull` where relevant.
  - `docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml up -d --build`.
- Print service URLs and basic diagnostics commands at the end.

## Credentials
- Do not commit real secrets.
- Move current local root `.env` usage toward local-only secrets and document that existing exposed OpenAI keys should be rotated.
- Production credentials live only at `/opt/fv-app/.env.production`.
- `init.sh` owns generation for infrastructure secrets and prompts for third-party credentials:
  - `OPENAI_API_KEY`
  - `TELEGRAM_BOT_TOKEN`, `TELEGRAM_BOT_USERNAME`
  - SMTP fields
  - YooKassa fields
  - `ADMIN_EMAILS`
  - `SUPPORT_EMAIL`
- Backend receives config through existing double-underscore env vars, so no backend configuration API changes are needed.

## Docs And Tests
- Update README with:
  - local dev startup.
  - production one-server startup.
  - DNS requirement for HTTPS: domain A-record must point to the server before running `init.sh`.
  - credential rotation note.
  - update/redeploy command: `cd /opt/fv-app/current && git pull && bash init.sh`.
- Verify:
  - `docker compose -f docker-compose.prod.yml config`.
  - frontend build still passes.
  - backend tests or at minimum backend build passes.
  - production compose starts and `/health` is reachable through the proxy.
  - frontend can call `/api/public/config` through same-origin routing.
