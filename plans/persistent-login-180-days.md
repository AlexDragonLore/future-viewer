# Вход на 180 дней

## Причина и изменения

- Production использует `Jwt:ExpiresMinutes=30` из appsettings. Frontend уже хранит JWT в localStorage и восстанавливает его после перезапуска браузера.
- Установить срок новых токенов 259200 минут (180 дней) в appsettings и fallback JwtOptions; явно передать настройку через Compose (`JWT_EXPIRES_MINUTES`).
- Сохранить проверку подписи, срока, состояния аккаунта, security_version и актуальных прав администратора. Сброс пароля отзывает старые токены, обычный выход очищает локальную сессию.
- Не добавлять автоматическое бесконечное продление. Действующие короткие токены сохраняют прежний срок; новый срок применяется после следующего входа/подтверждения email/сброса пароля.

## QA / verification

- Проверки подписанного JWT: срок 180 дней, согласованность expiresAt и exp, защита от истёкших и отозванных сессий.
- `dotnet test backend/FutureViewer.slnx`; существующие frontend-тесты восстановления сессии, logout, cookies и обработки HTTP401.
- Frontend runtime/верстка не меняются; проверить работоспособность public production страницы и health после backend-only выпуска.
- Проверить Compose и фактическую настройку Jwt__ExpiresMinutes=259200 внутри production-контейнера без вывода секретов. Production-вход в пользовательский аккаунт не требуется; выдача и отзыв токенов проверяются интеграционными тестами.

## Deploy

- База: выпущенный `4c03497` в активном worktree. Release branch `codex/mobile-admin-production`, target `main`; `git push origin HEAD:main` без force после проверки remote.
- Собрать amd64 backend image из проверенного SHA. Сохранить текущий frontend image под новым общим tag без пересборки; сохранить SHA, image references и env для отката.
- Сервер `deploy@80.87.104.245`, checkout `/opt/future-viewer`, env `/opt/fv-app/.env.production`. В env изменить только FV_IMAGE_TAG и JWT_EXPIRES_MINUTES=259200.
- Обновить checkout fast-forward. Из него: `sudo -n env FV_SHARED_EDGE_PORT=18080 docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-deps --no-build --pull never backend`.
- Проверить `sudo -n docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps`, `GET https://alex-taro.ru/health`, lifetime config; frontend/Caddy/DB и соседние контейнеры должны остаться прежними.
- Новых миграций нет. При проблемах восстановить предыдущие env, checkout и backend image, затем повторить health.
