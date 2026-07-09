# Current Release Main Deploy

## Summary
- Publish the implemented current-release changes to `main`.
- Deploy the updated `main` to the current production server.
- Verify production containers, `/health`, and a frontend browser smoke check.

## Deploy
- Branch/merge target: commit from the current working branch and push to `origin/main`. Remote `master` is absent; the production workflow is configured for both `main` and `master`.
- Push step: `git push origin HEAD:main`.
- Server/update command: GitHub Actions workflow `.github/workflows/deploy-production.yml` updates the server repository and runs Docker Compose with `/opt/fv-app/.env.production`, `docker-compose.prod.yml`, `docker-compose.prod.prebuilt.yml`, and project name `future-viewer`.
- Post-deploy verification:
  - `docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps`
  - `GET https://alex-taro.ru/health`
  - production browser smoke check for frontend-affecting changes.

## QA / Verification
- Before push:
  - `dotnet test backend/tests/FutureViewer.DomainServices.Tests/FutureViewer.DomainServices.Tests.csproj`
  - `dotnet test backend/tests/FutureViewer.Integration.Tests/FutureViewer.Integration.Tests.csproj`
  - `cd frontend && npm run type-check`
  - `cd frontend && npm test`
  - `cd frontend && npm run build`
- Browser QA already covered subscriber warning, free-user warning, and announcements locally; history delete browser confirm was limited by Browser Use native-dialog instability and is covered by frontend + backend tests.

## Assumptions
- `main` is the active production branch because it is the only default production branch present on `origin`.
- Existing deploy workflow configuration is authoritative for updating the production server.
