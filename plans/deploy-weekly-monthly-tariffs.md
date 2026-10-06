# Обновление тарифов на production

## Реализация

- Опубликовать тарифы 99 ₽ / 7 дней и 299 ₽ / 30 дней и согласованный дизайн на `alex-taro.ru`.
- Сохранить текущую production-топологию и настройки; обновить только backend/frontend. Тарифный релиз не меняет миграции, Caddy, сеть или volumes.
- Исправить обнаруженный CI-флап в guest integration test: PostgreSQL сохраняет timestamp с точностью микросекунды, поэтому сравнение ответа до сохранения и истории допускает 1 мкс; поля содержимого проверяются точно.

## QA / verification

- Основа релиза проверена: 398 backend-тестов, 302 frontend-теста, type-check/build, технические compliance checks и 78 браузерных сценариев.
- После test-only исправления выполнить focused guest integration tests в Release; проверить новый GitHub backend CI.
- Сохранить публичные frontend build args из production Compose, собрать `linux/amd64` images exact SHA. Никакие credentials не выводить и не помещать в build args.
- Перед обновлением сохранить env, точный Git revision и оба текущих image reference в приватной директории deployment state.
- После обновления выполнить обязательный `docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps`, `GET https://alex-taro.ru/health` и проверить оба тарифа в `/api/public/config`.
- Production rendered browser smoke: мобильная и desktop главная, гостевое предложение с обеими ценами, актуальный bundle/design; авторизованный выбор тарифа покрыт локальными API-фикстурами. Реальные оплаты и новые аккаунты для smoke не создавать.
- Убедиться, что сервисы janetka.ru и существующий edge не изменились.

## Deploy

- Ветка `codex/weekly-monthly-tariffs`, target `main`; commit/push test-only исправления и этого плана обычным fast-forward `git push origin HEAD:main`.
- Сервер `deploy@80.87.104.245`, checkout `/opt/future-viewer`, env `/opt/fv-app/.env.production`, project `future-viewer`.
- Сборка/загрузка обоих image с exact Git SHA, затем fetch/fast-forward серверного checkout к этому SHA.
- В runtime env установить `YUKASSA_WEEKLY_PRICE_AMOUNT=99`, `YUKASSA_MONTHLY_PRICE_AMOUNT=299`, `YOOMONEY_WEEKLY_PRICE_AMOUNT=99`, `YOOMONEY_MONTHLY_PRICE_AMOUNT=299`, сохранив другие параметры.
- Обновление: `FV_SHARED_EDGE_PORT=18080 FV_IMAGE_TAG=<sha> docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-deps --no-build --pull never backend frontend`.
- Workflow, меняющий edge-топологию, не запускать. При необходимости rollback восстановить env и точные предыдущие image reference обоих сервисов, не затрагивая БД и edge.
- Post-deploy: containers/health/config, production browser smoke и проверка janetka.ru.
