# Секретная игра ко дню рождения

## Implementation
- Добавить отдельный маршрут `/birthday` без публичного пункта меню, с `noindex` и отключённой аналитикой. Секретная ссылка передаёт ключ во фрагменте `#key=…`, автоматически открывает приглашение и сразу очищает адрес.
- Создать игру «Дом твоей мечты» для Даши: пять этапов архитектуры и таро, по 100 очков за правильный ответ; подсказки и повторные попытки без штрафов.
- Проверять ключ и ответы на сервере. Хранить прогресс в зашифрованном ограниченном по времени билете, восстанавливать его после перезагрузки.
- За 500 очков открыть поздравление и оригинальный сертификат Ozon на 15 000 ₽. Оба вложения хранить вне публичной директории и Git; выдавать только после прохождения.
- Сгенерировать ключ, настроить локальные секреты и подготовить инструкции для запуска. Сохранить существующие изменения в рабочем дереве.

## QA / verification
- `npm run type-check`, `npm test`, `npm run build`.
- Серверные тесты HTTP: неверный/отсутствующий ключ, подделанный/просроченный билет, пропуск этапов, неправильные ответы, повтор запроса, доступ к вложениям до и после прохождения, отключённая конфигурация и ограничение попыток.
- Playwright: мобильный и настольный размеры, клавиатура, перезагрузка, ошибки сети, весь путь до подарка.
- Проверка отрисованной страницы через in-app browser; просмотр мобильного и настольного скриншотов.
- Проверка отсутствия ключа и сертификата в публичной сборке.

### Local verification results
- Frontend type-check/build passed; all 357 unit tests passed; analytics/SEO regression subset (35 tests) passed after final changes.
- Backend solution: 460 tests passed, including eight birthday HTTP/security tests.
- Four real-API Playwright flows passed on desktop and mobile; mobile full journey repeated after visual polish. Keyboard activation, 320px overflow, reload, bad key, network retry, download and replay covered.
- Rendered in-app browser inspected invitation and game feedback. Desktop reward and mobile screenshots inspected.
- 278 public build files checked: no secret key or original private JPGs; birthday route absent from sitemap. Backend/frontend/release technical compliance checks passed; existing documentary review notices and dependency audit warnings remain.

## Deploy
Пользователь запросил публикацию и возможность самостоятельно проверить игру на production.
- Изолированная рабочая копия от `origin/main` (`24d7a32`), без незавершённых изменений основного рабочего каталога. Целевая ветка `main`; после QA `git push origin HEAD:main`.
- Сервер `deploy@80.87.104.245`, checkout `/opt/future-viewer`. Сохранить текущие Git SHA, ссылки backend/frontend images и env для отката. Сверить миграции: этот выпуск не меняет схему.
- Приватные JPG загрузить в `/opt/fv-app/birthday`, подключить read-only только backend. SHA-256 ключа и пути передать через серверный env. Не включать ключ и вложения в Git или Docker images.
- Собрать оба `linux/amd64` images из проверенного commit, frontend с подтверждёнными публичными production build arguments. Загрузить images на сервер, сверить checksum.
- Выполнить `git fetch origin main` и `git merge --ff-only <release-sha>`. Обновить только backend/frontend командой `sudo -n env FV_IMAGE_TAG=<release-sha> FV_SHARED_EDGE_PORT=18080 FV_LEGACY_EDGE_IP=<verified-caddy-ip> docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -f docker-compose.prod.birthday.yml -p future-viewer up -d --no-deps --no-build --pull never backend frontend`.
- Проверить `docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps`, `GET https://alex-taro.ru/health`, отсутствие доступа к подарку без билета, production browser smoke всей игры и главной страницы. Передать пользователю секретную ссылку; прохождение повторяемое, активация Ozon не выполняется.
