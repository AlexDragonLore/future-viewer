# Одна плашка оплаты для платного расклада

## Изменения

- Удалить дублирующий нижний блок «Полный доступ» и его стили из HomeView.
- Для неоплаченного платного расклада или исчерпанного бесплатного лимита показывать только SubscriptionBanner: без красного предупреждения и кнопки запуска расклада.
- Во время загрузки подписки показать нейтральный статус. Для доступного расклада вернуть обычную кнопку запуска; гостевое предложение и проверку вопросов сохранить.

## QA / verification

- Frontend unit tests, `npm run type-check`, `npm run build`; browser tests для платного выбора, исчерпанного лимита, активного доступа, переключения обратно на бесплатную карту и отключённых платежей.
- Rendered QA через in-app browser на 393px с изолированными fixtures: одна плашка, нет красного блока/лишней кнопки/повторного описания, элементы внутри экрана. Не запускать реальную оплату.
- Production mobile smoke и проверка опубликованной HomeView-сборки. Авторизованный production-сеанс при отсутствии доступа заменить браузерными fixtures и проверкой deployed bundle; не создавать реальные платежи/расклады ради QA.

## Deploy

- База `eba4714` из текущего release worktree; ветка `codex/mobile-admin-production`, target `main`. Проверенный commit отправить `git push origin HEAD:main` без force.
- Собрать amd64 frontend image с действующими публичными build values и номером Метрики. Сохранить current env/revision/images, backend image сохранить под новым tag без пересборки.
- Сервер `deploy@80.87.104.245`, checkout `/opt/future-viewer`, env `/opt/fv-app/.env.production`; заменить только FV_IMAGE_TAG.
- Обновить checkout fast-forward и выполнить `sudo -n env FV_SHARED_EDGE_PORT=18080 docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-deps --no-build --pull never frontend`.
- Проверить `sudo -n docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps`, `GET https://alex-taro.ru/health`, production browser smoke и ожидаемый bundle. Backend, DB, Caddy и соседние контейнеры не перезапускать.
- Если проверка не пройдёт, восстановить прежний env/checkout/frontend image; повторить health и browser smoke.
