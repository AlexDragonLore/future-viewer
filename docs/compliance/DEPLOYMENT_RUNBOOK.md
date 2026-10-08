# Обновление production alex-taro.ru

SSH production: `deploy@80.87.104.245`. Рабочий env: `/opt/fv-app/.env.production`, права `0600`; Compose project: `future-viewer`; checkout: `/opt/future-viewer`.

Осмотр сервера 3 октября 2026 подтвердил существующий публичный Caddy на 80/443, обслуживающий Future Viewer и `janetka.ru`. Контейнеры находятся в `future-viewer_default`; текущий Caddy имеет IP `172.18.0.5`. При этом обновлении используется `docker-compose.prod.legacy-edge.yml`, сохраняющий external сеть и прежние TLS volumes; миграция на отдельный внешний edge не выполняется. Не запускать стандартный production Compose/workflow без этого override: он описывает другой, пока не внедрённый сетевой layout.

Этот файл описывает процедуру. Успех текущего production-обновления фиксируется отдельно после проверки сервера и сайта.

## Что сохранить при обновлении

- Существующие AI/payment/mail credentials, `EMAIL_TRANSPORT=RegruWebmail`, DB/JWT значения и явно заданные operational flags. `init.sh` предназначен для установки; для обновления существующий env не пересоздавать и не подменять `.env.production.example`.
- Функции используют реальный официальный HTTPS endpoint и credentials; документарные `manual_approved` и `VITE_NPD_RECEIPT_PROCESS_VERIFIED=false` не отключают регистрацию, AI или оплату. Неизвестные сведения остаются незаполненными. Явный `AI_ENABLED=false`, `PAYMENT_ENABLED=false` или `PAYMENT_WEBHOOK_ENABLED=false` продолжает действовать.
- Тот же project name и named volumes `pgdata`, `data-protection-keys`. Backend image задаёт `DataProtection__KeysPath=/app/data-protection-keys`; volume должен оставаться доступен на запись пользователю приложения при `read_only` root filesystem. Не удалять/пересоздавать volume: иначе ранее выданные guest tickets перестанут расшифровываться.
- Публичные `VITE_*` build values отдельно от backend secrets: frontend получает их во время сборки, изменение runtime env не меняет готовый JS. Настоящий номер Метрики задаётся только после создания счётчика; без номера аналитика остаётся выключена.

## Доверенные прокси и платёжные уведомления

`ReverseProxy:KnownProxies` по умолчанию пуст: backend игнорирует `X-Forwarded-For`/`X-Forwarded-Proto`. Тогда клиентские лимиты используют адрес внутреннего proxy, а source check YooKassa может отклонить уведомление с HTTP 401. Service `env_file` в production Compose передаёт `ReverseProxy__KnownProxies__0`, `ReverseProxy__KnownProxies__1` и `ReverseProxy__ForwardLimit`, если они действительно присутствуют в серверном env.

В текущей цепочке «публичный Caddy → backend» только один доверенный переход. Caddy принимает публичный запрос напрямую и сохраняет стандартное игнорирование входящих `X-Forwarded-*`; `trusted_proxies` в Caddy здесь не нужен. Legacy override закрепляет его подтверждённый адрес через `FV_LEGACY_EDGE_IP`; backend получает тот же exact IP через `ReverseProxy__KnownProxies__0` и `ReverseProxy__ForwardLimit=1`. Перед выпуском проверить существующую сеть и владельца адреса: при осмотре `172.18.0.5` принадлежал Caddy в сети `172.18.0.0/16` с gateway `172.18.0.1`. Не назначать IP, занятый другим контейнером. После recreate повторно сверить фактический IP. Не доверять целой приватной сети и не угадывать адрес. См. [официальное поведение Caddy](https://caddyserver.com/docs/caddyfile/directives/reverse_proxy#defaults).

Проверить запрос с поддельным `X-Forwarded-For`: он не должен пройти source check. Подтверждение фактического paid callback проверять отдельно; успешное создание checkout и redirect не доказывают оплату или активацию доступа. При будущей миграции на два proxy потребуется отдельно настроить доверие на обоих переходах.

## Перед запуском нового backend

1. Завершить backend tests, frontend type-check/test/build, rendered browser QA и проверки `check-compliance.mjs` для backend/frontend/release. Собрать оба image из одного проверенного SHA и push этого commit в `main`.
2. На сервере сверить checkout revision, фактические backend/frontend image, состояние project containers, env mode, volume mounts и proxy chain. Не печатать полный env, `docker inspect .Config.Env` или вопросы пользователей.
3. Сохранить previous Git revision, отдельные backend/frontend/Caddy image references, env и фактический Caddy config в защищённом каталоге выпуска. Текущий server `deploy/Caddyfile` изменён локально: сохранить его до обновления checkout, не затирать dirty файл вслепую. Не предполагать, что старые frontend/backend имеют одинаковый tag.
4. Сверить `__EFMigrationsHistory` с миграциями нового SHA. Backend запускает `Database.MigrateAsync` при startup, поэтому первый restart уже может изменить схему. До него сделать доступную для восстановления резервную копию БД и проверить её восстановление в изолированной БД. Готовые streaming encryption scripts и условия их использования описаны в [backup/restore runbook](BACKUP_RESTORE_RUNBOOK.md); существующие backup credentials не заменять и не публиковать.
5. Для `20261003122747_RemoveTelegramIntegration` учесть удаление Telegram columns, achievement/grants и отзыв разрешений. `Down` не восстанавливает удалённые значения. После этой миграции старый backend может обращаться к уже удалённым columns, поэтому image-only rollback не считается безопасным.

## Ручной выпуск prebuilt images

Этот путь использует уже настроенный сервер и не требует копирования рабочих credentials в GitHub Secrets. Исторический `/private/tmp/fv-regru-smoke/deploy-verified.sh` содержит прежний image guard и email overlay; его не запускать повторно без адаптации под текущие facts и оба новых image.

1. Собрать и загрузить на сервер backend/frontend images с tag полного проверенного SHA. Для frontend передать только подтверждённые публичные build values. Сохранить image archive checksum; проверить наличие обоих загруженных image.
2. `git fetch origin main`; проверить, что `origin/main` равен выбранному SHA, затем обновить серверный checkout fast-forward. При неожиданном старом image/revision или dirty server checkout остановить замену и выяснить причину.
3. Сохранить текущий env. Если нужен узкий overlay, менять только согласованные значения; пустые источники не стирают существующие credentials. Валидировать итоговую копию до атомарной замены env. После проверки адреса и его принадлежности текущему Caddy задать `FV_LEGACY_EDGE_IP=172.18.0.5`, `ReverseProxy__KnownProxies__0=172.18.0.5`, `ReverseProxy__ForwardLimit=1`. Если фактический адрес отличается, сначала выяснить причину и использовать проверенный адрес; не выбирать чужой занятый IP. Сохранить проверенный Caddy config по `/opt/fv-app/legacy-edge.Caddyfile`, используя `deploy/legacy-edge.Caddyfile.example` с дословно сохранённым `janetka.ru` block. Валидировать его тем же Caddy binary до reload/recreate. Проверить существование external сети `future-viewer_default` и volumes `future-viewer_caddy_data`, `future-viewer_caddy_config`.
4. Убедиться, что сделана backup/restore проверка, а Compose поддерживает `!override` (2.24.4+). В фактическом checkout выполнить команды ниже с выбранным SHA. На текущем сервере Docker может требовать `sudo -n`; использовать подтверждённый доступ. `config --quiet` не печатает env. `FV_SHARED_EDGE_PORT` нужен для интерполяции base файла, но его loopback port удалён legacy override.

```bash
cd /opt/future-viewer
export FV_IMAGE_TAG='<полный проверенный SHA>'
export FV_SHARED_EDGE_PORT=18080
bash deploy/validate-runtime-env.sh --env-file /opt/fv-app/.env.production
docker image inspect "future-viewer-backend:$FV_IMAGE_TAG" >/dev/null
docker image inspect "future-viewer-frontend:$FV_IMAGE_TAG" >/dev/null
docker compose --env-file /opt/fv-app/.env.production \
  -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml \
  -f docker-compose.prod.legacy-edge.yml \
  -p future-viewer config --quiet
docker compose --env-file /opt/fv-app/.env.production \
  -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml \
  -f docker-compose.prod.legacy-edge.yml \
  -p future-viewer up -d --no-deps --no-build --pull never backend frontend caddy
```

Команда заменяет application services и существующий Caddy в том же project; PostgreSQL, external сеть, TLS storage и контейнер `janetka-site` не пересоздаются. Caddy restart кратко затрагивает оба домена, поэтому сразу проверить и `janetka.ru`. Не выполнять `down -v`, `--remove-orphans`, global Docker prune или слепое изменение соседнего проекта.

## Проверка после выпуска

```bash
docker compose --env-file /opt/fv-app/.env.production \
  -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml \
  -f docker-compose.prod.legacy-edge.yml -p future-viewer ps
curl -fsS https://alex-taro.ru/health
curl -fsSI https://janetka.ru/
```

Проверить startup/migrations без вывода секретов и пользовательского текста, отсутствие Data Protection write errors, expected new image references и безопасность заголовков. HTTP 2xx `/health` подтверждает ответ приложения, но не внешние providers.

В production browser проверить первый бесплатный расклад на три карты без аккаунта, половину толкования, регистрацию с доставкой подтверждения и продолжение того же расклада после подтверждения почты. Проверить, что первый расклад занимает дневной лимит, со следующего дня бесплатно доступна одна карта, а удаление раскладов не возвращает первый расклад. Также проверить профиль и отсутствие Telegram. Guest ticket должен сохраниться после restart. Метрика не загружается до согласия; цели проверяются только при реально заданном counter ID. SEO sitemap/robots и Webmaster tag должны быть доступны.

Платёжный smoke сначала проверяет создание checkout без списания. Для полного подтверждения отдельно выполнить разрешённый тестовый платёж/официальную проверку провайдера и увидеть правильный серверный статус конкретного order, однократную активацию доступа и цель `payment_completed`. Не помечать покупки проверенными по success redirect, существующей подписке или ручному webhook POST.

## Откат

До изменения схемы можно вернуть сохранённые checkout revision, отдельные старые image references, env и application proxy config, сохраняя project/volumes; после этого повторить containers/health/browser QA. Удаление Telegram делает автоматический rollback только image/env потенциально несовместимым с БД: перед использованием workflow rollback сверить применённые миграции.

После необратимой миграции предпочтителен forward fix. Если требуется DB restore, остановить только трафик и application services Future Viewer, сначала проверить backup в isolated environment, затем согласованно восстановить БД и соответствующие image/env. Учесть потерю записей после backup, успешные платежи и повторное применение deletion ledger; не выполнять слепой restore поверх работающей БД. Подробности — в [rollback runbook](ROLLBACK_RUNBOOK.md).
