# Backup, rotation и restore drill

Статус: код для потокового шифрованного backup и изолированного restore test добавлен, но backup provider, физическая страна хранения, ключи, срок ротации и результат первого восстановления неизвестны. Наличие файлов backup нельзя считать подтверждённым.

## До включения

1. Подтвердить документами, что первичная БД, snapshots и backup storage находятся в РФ; внести hosting/backup providers в `config/processors.json`.
2. Утвердить срок в `config/retention.json` и выставить тот же целочисленный срок в `FV_BACKUP_RETENTION_DAYS`.
3. Создать отдельную age-пару ключей только для Future Viewer. Public recipient хранить в защищённой operational config, identity — вне application host либо в approved secrets manager. Не помещать identity в repo/env приложения.
4. Создать `/opt/fv-app/backups/future-viewer` с владельцем backup service account и mode `0700`. Каталог janetka.ru должен быть другим.
5. Проверить квоты, alert на неуспешный backup и защищённое копирование во второй подтверждённый storage.

## Создание backup

Скрипт передаёт `pg_dump --format=custom` непосредственно в `age`; незашифрованный dump на диск не записывается.

```bash
cd /opt/fv-app
FV_BACKUP_AGE_RECIPIENT='age1…' \
FV_BACKUP_RETENTION_DAYS='<утверждённое число дней>' \
FV_ENABLE_BACKUP_ROTATION=false \
./scripts/backup-postgres.sh
```

Первую ротацию включать только после подтверждённого restore drill и сверки срока:

```bash
FV_ENABLE_BACKUP_ROTATION=true ./scripts/backup-postgres.sh
```

Результат — `.dump.age` и SHA-256 encrypted artifact. Filename не содержит email, user ID, вопрос или иной идентификатор субъекта.

## Изолированное восстановление

Restore test не подключается к production network/DB: создаётся одноразовый container с `--network none`, после проверки он удаляется.

```bash
FV_BACKUP_AGE_IDENTITY_FILE='/secure/off-host/future-viewer-age-key.txt' \
./scripts/restore-test-postgres.sh \
  /opt/fv-app/backups/future-viewer/future-viewer-postgres-<UTC>.dump.age
```

Записать дату, hash backup, версию Postgres, длительность, число восстановленных public tables и итог без строк данных. Не писать содержимое таблиц в журнал проверки.

## Удаление пользователя и backup cycle

Немедленное точечное редактирование immutable backup запрещено. Удаление выполняется в рабочей БД; deletion job/tombstone должен повторно применяться после аварийного восстановления, а backup окончательно исчезает по утверждённой ротации. Срок и процедура должны совпадать с опубликованной политикой. Legal evidence и финансовые записи отделяются только после определения законного состава и срока.

## Аварии

- При утрате identity backup считается невосстановимым; активировать incident process.
- При компрометации identity остановить копирование, сменить recipient, пере-зашифровать допустимые active backups и зафиксировать уничтожение старых копий.
- При неуспешном restore запретить migration/deploy, создать новый backup и устранить причину; не удалять последнюю подтверждённую копию.
