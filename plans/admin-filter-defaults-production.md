# Admin message defaults and compact filters

## Implementation
- Open the message list with personal unread filtering and oldest-first sorting. Account reset restores these defaults.
- Use one unread/all segmented control and labeled author/sort selects, with consistent touch targets and mobile spacing. Preserve searching, pagination and automatic view receipts.
- Keep unrelated changes in the primary checkout untouched; use the attached admin-filter-defaults worktree based on main at 87cae8e.

## QA / verification
- Completed: frontend type-check, all 355 unit tests, production build, and 18 desktop/mobile admin browser scenarios. In-app browser inspected the actual component with synthetic fixtures at 320 and 390 px; switching and overflow checked.
- Require the full frontend GitHub check on the exact release SHA before deployment. No backend changes or migrations are included, so backend tests are not repeated.
- Post-deployment: public health and browser smoke; render deployed admin assets against intercepted synthetic API responses and verify defaults, filtering, ordering and the unread queue. This is deployed frontend verification, not a real production administrator session.

## Deploy
- Target branch: main. Commit the five changed frontend source/test files and this plan; push with git push origin HEAD:main after checking the current remote baseline.
- Use a temporary delivery branch and the existing GitHub Actions deployment identity. Pin source to the tested main SHA and preserve public frontend build arguments from the server. Build linux/amd64 frontend, verify archive checksum and loaded image configuration digest.
- Production: deploy@80.87.104.245, checkout /opt/future-viewer, protected configuration /opt/fv-app/.env.production. Record the current Git revision, environment and frontend image for rollback. Verify no backend/configuration changes; alias the unchanged backend image under the release tag so the Compose image model remains complete.
- Fast-forward the server checkout to the fixed release and update only FV_IMAGE_TAG. Run sudo -n env FV_IMAGE_TAG=<sha> FV_SHARED_EDGE_PORT=18080 FV_LEGACY_EDGE_IP=<verified-current-caddy-ip> docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-deps --no-build --pull never frontend.
- Verify docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps and GET https://alex-taro.ru/health, exact frontend image, published filter assets, unchanged backend/database/proxy/neighbor containers and production browser smoke.
- Roll back the frontend image, saved environment and checkout on deployment verification failure. No database writes or migrations are part of this release. Remove the temporary delivery branch after success.
