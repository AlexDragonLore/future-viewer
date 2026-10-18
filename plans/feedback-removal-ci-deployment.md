# Publish retired feedback removal using the established CI delivery

## Release and scope

- Publish the tested PR11 release ace5ed21c70b11d9e8b986db366db9034e022feb to origin/main without force, preserving its successful frontend and backend checks.
- Use a separate one-shot codex/feedback-retirement-delivery branch based on the proven October17 CI delivery. The workflow builds the pinned release checkout; operational scripts do not merge into the application release.
- Reuse the existing PRODUCTION_DEPLOY_SSH_KEY and verified host key. Do not create credentials or modify provider/account configuration.

## QA / verification

- Existing release validation:448 backend tests,350 frontend unit tests, type-check/build,116 GitHub browser scenarios and rendered in-app browser checks for admin/profile/legacy links.
- Require successful GitHub frontend/backend checks on the exact release SHA before delivery. Check workflow YAML, shell syntax and embedded Python locally and review migration-aware recovery.
- Verify transferred image archive checksums, archived image configuration digests and loaded image content.
- Before application restart, back up the production database in a protected server-side release record, verify an isolated restore, apply the exact single new migration on that restore, and check preserved application records and retired table/achievements.
- After deployment: docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps; GET https://alex-taro.ru/health; expected migration, removed API404s and protected admin401; production browser smoke including retired link redirects. Use mocked local admin authorization for UI if production admin session is unavailable; clearly distinguish it from live authorization.

## Deploy

- App target:origin/main at ace5ed21c70b11d9e8b986db366db9034e022feb. Push the tested commit fast-forward; mark PR11 ready and verify merged status.
- Push delivery branch, then gh workflow run deploy-production.yml --ref codex/feedback-retirement-delivery.
- Workflow uses deploy@80.87.104.245, actual checkout /opt/future-viewer and env /opt/fv-app/.env.production. Preserve current public frontend build arguments.
- Confirm previous revision c2543b3f6a44544e269dc9aa178f7ed953937b31, its recorded image IDs, actual shared Caddy address and existing unrelated-container state. Update only backend/frontend with prebuilt+legacy-edge Compose overrides and --no-deps --no-build --pull never.
- Allow exactly migration20261018073538_RemoveRetiredFeedback. Preserve environment values except release image tag, Caddy, Postgres, neighboring site, volumes and networks.
- If failure occurs before schema change, previous application images remain a valid rollback. If migration history changes, do not start the incompatible old backend or automatically overwrite the live database; keep protected recovery artifacts for a forward fix or deliberate recovery.

## Pre-dispatch results

- PR11 merged by fast-forward push to main at ace5ed21c70b11d9e8b986db366db9034e022feb.
- Delivery workflow YAML, nine shell blocks and embedded Python syntax checks passed; independent workflow/script review found no blockers.
- Exact BOM-free migration SQL rehearsed in an isolated local PostgreSQL17: retained records and unrelated achievement grants preserved, retired table/codes removed, migration recorded.
- Production-state guards pin the verified previous image IDs from successful CI run39013037553. The CI runner will revalidate them before mutation.

## Production result — 18 October 2026, 12:12 MSK

- GitHub Actions delivery39114202557 succeeded from workflow revision8702ced051eaaa661287b9b7f69ff852477a35b6, deploying application ace5ed21c70b11d9e8b986db366db9034e022feb.
- CI SSH succeeded despite the local connection timeout. Encrypted database backup, isolated restore and migration rehearsal passed; retained table counts were unchanged in the rehearsal.
- Live migration20261018073538_RemoveRetiredFeedback applied; feedback table and four related achievement codes removed. Backend/frontend image identity, preserved mounts/networks, shared Caddy and neighboring containers verified.
- Required Compose ps command executed successfully. Public health200/database ready; retired feedback API404; protected admin readings401; public catalog contains seven remaining achievements.
- Independent public asset verification: /assets/index-BROMvtYT.js contains counter113366341 and no retired feedback view/table references. No test goals, accounts, readings or payments created.
- Main frontend/backend GitHub checks passed, including116 browser scenarios. Production in-app browser verified rendered homepage and legacy feedback redirect; authenticated admin/profile removal was covered by earlier synthetic browser QA.
