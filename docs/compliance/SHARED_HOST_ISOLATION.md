# Изоляция alex-taro.ru и janetka.ru на общем сервере

Статус на 1 августа 2026 года: repository-side конфигурация Future Viewer усилена, но фактическая конфигурация общего сервера и janetka.ru не проверена. До выполнения этого runbook production deployment заблокирован.

## Целевая граница

- Единственный host-level reverse proxy владеет публичными `80/443` и имеет отдельные exact-host site blocks для `alex-taro.ru` и `janetka.ru`.
- Compose-проект этого репозитория называется только `future-viewer`; внутренний Caddy слушает только `127.0.0.1:$FV_SHARED_EDGE_PORT`.
- Репозиторий, env, backup и deployment state Future Viewer находятся только под `/opt/fv-app`; каталоги janetka.ru не являются bind mount ни одного контейнера Future Viewer.
- PostgreSQL Future Viewer не публикует порт и подключён только к внутренней сети `future-viewer_data`; у janetka.ru должны быть иной контейнер, volume, база, DB user и пароль.
- Backend подключён к `proxy`, `data`, `egress`; frontend — только к `proxy`; PostgreSQL — только к `data`. Общих Docker networks/volumes быть не должно.
- Docker log rotation задаётся отдельно каждому сервису. Backup directory, encryption recipient и identity не переиспользуются между проектами.
- Cookie/domain policy другого проекта не должна использовать `.ru`, общий parent domain или `alex-taro.ru`. Future Viewer сейчас использует browser storage origin `https://alex-taro.ru`, что само по себе изолировано от `janetka.ru` same-origin policy.

## Ручная настройка shared edge

1. Зарезервировать неиспользуемый loopback port, например `18080`, и записать его как `FV_SHARED_EDGE_PORT` только в `/opt/fv-app/.env.production`.
2. Добавить в фактический host-level Caddy конфигурацию, эквивалентную `deploy/shared-edge/alex-taro.caddy.example`. Не заменять и не объединять site block janetka.ru.
3. Проверить полный конфиг командой `caddy validate --config <фактический-Caddyfile>` и выполнить graceful reload shared edge.
4. Проверить, что `ss -lntp` показывает loopback bind Future Viewer и только shared edge слушает публичные `:80/:443`.
5. Не запускать `docker system prune -af`, `docker image prune -af`, удаление сетей/volumes без project label или wildcard-команды на общем host.
6. Удалить URI/query из access-log формата shared edge либо отключить access log для bearer routes; задать отдельный alex-taro.ru sink и TTL. Не направлять его в log janetka.ru.

## Проверка после запуска

Запускать от отдельного deploy-пользователя с доступом только к `/opt/fv-app` и Docker-командам, необходимым workflow:

```bash
cd /opt/fv-app
FV_ENV_FILE=/opt/fv-app/.env.production \
FV_COMPOSE_PROJECT_NAME=future-viewer \
FV_OTHER_COMPOSE_PROJECT_NAME=janetka \
FV_REQUIRE_OTHER_PROJECT=true \
./scripts/check-project-isolation.sh
```

Скрипт не выводит значения секретов. Он блокирует non-loopback published ports, общие networks/volumes/mounts, повторное использование обнаруживаемых DB/JWT credentials и env-файл вне выделенного каталога. Это не заменяет ручную проверку host ACL, firewall, backup storage, Docker socket access и содержимого конфигурации janetka.ru.

Дополнительно проверить оба hostname и защиту от wrong-host routing:

```bash
curl -fsSI https://alex-taro.ru/
curl -fsSI https://janetka.ru/
curl -kSI --resolve alex-taro.ru:443:127.0.0.1 https://alex-taro.ru/
```

Зафиксировать в закрытом акте: container IDs, project labels, network/volume names, DB names/users без паролей, пути env/log/backup, владельцев и права каталогов. Не помещать секреты и персональные данные в акт.
