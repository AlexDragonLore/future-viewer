# Natural tarot reading voice

Replace formulaic interpretation prose with warm, concrete Russian that answers the user's question through the drawn cards. Remove mandatory generic reflection headings, repeated disclaimers, and unsolicited psychologist referrals for ordinary relationship concerns.

## Implementation

- Replace the shared interpreter prompt used by synchronous and streaming readings; keep all card positions, deck context, and grounded uncertainty.
- Pass only the existing safe suggested question after an acknowledged rewrite, without adding a second boilerplate style instruction.
- Keep privacy filtering, dangerous-question handling, and the interface's AI disclosure. Do not impersonate a human or claim certain knowledge of future events or private thoughts.
- Existing saved readings are unchanged; the new style applies to newly generated interpretations.

## QA / verification

- Build and run backend tests, including normal and streaming flows, rewrite handling, privacy and dangerous-question cases.
- Run backend and release technical checks.
- Generate a small set of synthetic readings with the configured production provider using the exact new prompts; review natural phrasing, question relevance, card coverage, and absence of boilerplate. Keep credentials on the server and synthetic evaluation output outside the application database.
- Verify production health, exact image/revision, startup logs, and unchanged neighboring containers after deploy.

## Deploy

- Branch `codex/natural-tarot-readings` from current `origin/main`; push the checked commit and fast-forward `main`.
- Build the backend amd64 image from the exact revision. Preserve the current frontend image, secrets, database, and existing public proxy.
- Update `/opt/future-viewer` on `deploy@alex-taro.ru` using `git fetch origin main` and `git merge --ff-only origin/main`.
- Preserve the previous backend image/environment for rollback. No database migration is added.
- Update the backend with `docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-deps --no-build --pull never backend`, using sudo and the new full SHA image tag.
- Check Compose status, `https://alex-taro.ru/health`, startup logs and neighboring container identities. Confirm the new prompt is in the deployed backend.
