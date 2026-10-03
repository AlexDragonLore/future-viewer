# Rollback production

Workflow до изменения production сохраняет previous image tag, exact Git revision и копию env в `/opt/fv-app/deployments`. При любом последующем сбое job он пытается вернуть revision/Compose/config, оба image и env автоматически. Успешный rollback не отменяет факт инцидента и не восстанавливает необратимо изменённую схему БД.

## Немедленное безопасное отключение интеграций

При утечке либо неизвестном processor сначала выставить в `/opt/fv-app/.env.production`:

```dotenv
AI_ENABLED=false
TELEGRAM_ENABLED=false
PAYMENT_ENABLED=false
PAYMENT_WEBHOOK_ENABLED=false
PRIVACY_EXPORT_ENABLED=false
```

Не печатать env и secrets в терминал/тикет. Перезапустить только project `future-viewer` и проверить flags через поведение API без реальных персональных данных.

## Ручной rollback image/env

```bash
cd /opt/fv-app
FAILED_TAG='<sha неуспешного выпуска>'
PREVIOUS_TAG="$(cat "/opt/fv-app/deployments/previous-tag-$FAILED_TAG")"
PREVIOUS_REVISION="$(cat "/opt/fv-app/deployments/previous-revision-$FAILED_TAG")"
test -n "$PREVIOUS_TAG"
git cat-file -e "$PREVIOUS_REVISION^{commit}"
git switch --detach "$PREVIOUS_REVISION"
install -m 600 "/opt/fv-app/deployments/env-before-$FAILED_TAG" /opt/fv-app/.env.production
FV_IMAGE_TAG="$PREVIOUS_TAG" docker compose \
  --env-file /opt/fv-app/.env.production \
  -f docker-compose.prod.yml \
  -f docker-compose.prod.prebuilt.yml \
  -p future-viewer \
  config --quiet
FV_IMAGE_TAG="$PREVIOUS_TAG" docker compose \
  --env-file /opt/fv-app/.env.production \
  -f docker-compose.prod.yml \
  -f docker-compose.prod.prebuilt.yml \
  -p future-viewer \
  up -d --no-build --remove-orphans
docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps
curl -fsS https://alex-taro.ru/health
```

После rollback рабочее дерево намеренно остаётся в detached state на последней рабочей revision; следующий workflow переключит его обратно на `main` только после прохождения release gates. Не удалять файлы `deployments/*` до закрытия incident.

Не выполнять global Docker prune: предыдущие image нужны для rollback, а host может обслуживать janetka.ru.

## Миграции БД

Backend пока запускает EF migration при старте. Перед релизом обязательно сделать подтверждённый encrypted backup и restore drill. Если migration необратима либо старый binary несовместим с новой схемой, не запускать image rollback вслепую:

1. Отключить публичный трафик только для alex-taro.ru на shared edge.
2. Остановить только `future-viewer` application containers, не janetka.ru и не общесерверный edge.
3. Сохранить forensic copy/log metadata без вопросов/ответов/секретов.
4. Восстановить отдельную проверенную backup-копию в isolated environment и проверить migration path.
5. Выполнить approved forward fix либо controlled database restore с учётом deletion ledger и потери транзакций после backup.
6. Повторить container, isolation, health, API и browser QA; оформить incident/postmortem.

Rollback юридических документов требует новой корректной версии/даты и не должен переписывать ранее зафиксированные consent evidence.
