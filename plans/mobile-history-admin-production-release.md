# Выпуск мобильных исправлений и админки

## Объём

- От актуальной `origin/main` (`1ab776b`) перенести исправления ширины профиля/истории, удаление галочки сохранения и панели ИИ из формы расклада, постоянное сохранение в аккаунте и мягкое удаление истории, просмотр последних сообщений и улучшения админки.
- Сохранить опубликованные изменения голосa ИИ, гостевого платного предложения, миграции включения истории по умолчанию, текущие workflow, credentials, Caddy и соседний сайт.
- Исходную рабочую папку с незакоммиченными изменениями не переключать и не очищать. Выпуск подготовить в отдельном worktree.

## QA / verification

- На итоговом дереве релиза: `dotnet test backend/FutureViewer.slnx`, `npm test`, `npm run type-check`, `npm run build`, Playwright для home/guest/profile/history/admin.
- Технические проверки репозитория: `git diff --check`, compliance backend/frontend/release и валидность Compose без вывода env.
- Проверить, что production bundle не содержит галочку/плашку главной формы, но сохраняет оплату и гостевой сценарий.
- Rendered QA через in-app browser на production при мобильной ширине: главная, форма авторизованного пользователя при доступном сеансе, профиль/история/админка при доступном сеансе. Если production-сеанс недоступен, проверить публичную страницу и deployed bundle; авторизованные состояния дополнительно проверены локально через изолированные fixtures.

## Deploy

- Release branch `codex/mobile-admin-production`, merge target `main`; push проверенного SHA командой `git push origin HEAD:main` без force после проверки, что remote не изменился. Текущий production workflow запускается вручную; использовать действующую ручную схему выпуска.
- Сервер `deploy@80.87.104.245`, checkout `/opt/future-viewer`, env `/opt/fv-app/.env.production`, Compose project `future-viewer`.
- До обновления сохранить точную server revision, отдельные frontend/backend image references и защищённую копию env. Сверить миграции; новых миграций этот релиз не добавляет. Существующие Caddy/DB/volumes не пересоздавать.
- Собрать SHA-tagged amd64 images, сохранив проверенные публичные frontend build values (включая Metrika); загрузить архив, проверить checksum, выполнить `docker load`, обновить checkout fast-forward до проверенного SHA.
- Команда обновления из `/opt/future-viewer`: `sudo -n env FV_IMAGE_TAG=<sha> docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-build --pull never --no-deps backend frontend`.
- После: `sudo -n docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps`, `GET https://alex-taro.ru/health`, browser smoke, проверка версии bundle и baseline `https://janetka.ru/`.
- При проблеме вернуть только backend/frontend на сохранённые image references и checkout, затем повторить health/browser smoke. Не выполнять down, удаление volumes или глобальную очистку Docker.
