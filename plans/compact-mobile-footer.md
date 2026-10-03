# Compact mobile footer

## Implementation

- Replace the long stack of mobile pills with compact text navigation.
- Keep About, FAQ, support and cookie settings readily accessible. Group the eight legal links in a native disclosure labelled “Документы”, closed initially; preserve the existing document URLs and cookie settings action.
- Keep comfortable tap targets, visible focus, text wrapping and a short collapsed footer at 320 and 390 px. Expanded documents stay in the page flow.

## QA / verification

- Run frontend type-check, existing component/cookie tests and production build. Run the relevant existing browser scenarios affected by moving document links; no backend changes, so prior backend verification remains applicable.
- Render in the in-app browser at 320×740, 390×844 and desktop. Verify collapsed height, no horizontal overflow, accessible expansion, document navigation, and cookie settings opening. Save a mobile screenshot showing the footer in page context.
- After deployment repeat rendered mobile/desktop footer checks and verify a document opens, cookies preferences remain accessible, containers are running and production health is OK.

## Deploy

- Reuse attached /Users/aleksandr/.codex/worktrees/production-guest-seo/future-viewer, branch codex/production-guest-seo; push branch and fast-forward remote main.
- Build an amd64 frontend image tagged with the final Git SHA, preserving the current public frontend build settings. No counter ID is invented.
- Server deploy@80.87.104.245, checkout /opt/future-viewer, env /opt/fv-app/.env.production. Record the running frontend image for immediate frontend-only rollback; preserve backend, database, Caddy, networks and existing credentials.
- Upload/load the frontend image; update the server checkout to exact main SHA; update FV_IMAGE_TAG and recreate frontend only using docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-build --pull never --no-deps frontend.
- Required post-deploy: docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps; GET https://alex-taro.ru/health; production browser smoke and janetka.ru check.
