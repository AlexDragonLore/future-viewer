# Публикация коммита тарифов

## Реализация

- Использовать чистый managed worktree от актуальной `origin/main`, ветка `codex/weekly-monthly-tariffs`.
- Перенести только тарифы 99 ₽ / 7 дней и 299 ₽ / 30 дней, согласованный дизайн и связанные тесты/конфигурации.
- Сохранить последующие исправления удалённой ветки: единственная плашка оплаты на главной, гостевое предложение, предложение оплаты после результата и остальные изменения.
- Создать коммит и выполнить обычный fast-forward push `git push origin HEAD:main`; исходную папку с локальными изменениями не сбрасывать.

## QA / verification

- В итоговом worktree выполнить `npm run type-check`, `npm test`, `npm run build` и полный `dotnet test backend/FutureViewer.slnx` с Docker/Postgres.
- Выполнить полный Playwright suite Chromium/mobile Chromium; проверить оба тарифа, последний UX главной, профиль и предложение оплаты на результате.
- Выполнить rendered browser QA с in-app browser на локальной странице; авторизованные состояния покрыть API-фикстурами Playwright. Просмотреть скриншоты.
- Проверить JSON/YAML/Compose, `git diff --check` и состав коммита. После push сверить SHA `origin/main`.

## Deploy

- Merge target и push target: `main`.
- Запрошен push. Текущий `deploy-production.yml` запускается только через `workflow_dispatch`, поэтому автоматического обновления сервера от push нет.
- Ручное обновление сервера не входит в этот запрос и не запускается. При отдельном запросе на deploy использовать checkout `/opt/future-viewer`, env `/opt/fv-app/.env.production`, Compose project `future-viewer` и exact commit images.
- Команда обновления после сборки/загрузки образов и установки новых цен в runtime env: `FV_SHARED_EDGE_PORT=18080 FV_IMAGE_TAG=<sha> docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-build --pull never backend frontend`.
- Post-deploy verification при публикации на сервер: `docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps`, `GET https://alex-taro.ru/health` и production browser smoke.

## Результаты QA

- Полный `dotnet test backend/FutureViewer.slnx`: 398 прошли, ошибок и пропусков нет, интеграционные тесты выполнены с Docker/Postgres.
- `npm run type-check`, `npm test` (302 теста) и `npm run build` прошли.
- Полный Playwright suite: 77 прошли; один существующий мобильный тест юридических страниц превысил 30 секунд при параллельном прогоне и прошёл в изолированном повторе за 3 секунды. Все 78 сценариев проверены; все 54 сценария тарифов, главной и профиля прошли с первого запуска.
- In-app browser подтвердил вид компонента из итогового worktree. Скриншоты главной и профиля с локальными API-фикстурами просмотрены.
- `node scripts/check-compliance.mjs --scope=frontend`, `--scope=backend`, `node --test scripts/tests/*.test.mjs` (10 тестов), JSON/YAML/Compose validation и `git diff --check` прошли.
