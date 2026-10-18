# Проверка возвращаемости, конверсий и публикация удаления отзывов

## Объём

- Проверить фактические показатели счётчика Яндекс Метрики 113366341: посещения, новые/вернувшиеся посетители и цели по датам. Различать возвращаемость за период и когортное удержание D1/D7; не интерпретировать неполный текущий день как падение.
- Проверить опубликованный код счётчика, согласие на аналитику и реальные запросы целей. Исправлять только воспроизведённые дефекты; отделить ошибки сайта от ограничений атрибуции Яндекса.
- Удалить отзывы поверх актуального origin/main в отдельном worktree: интерфейс, API, оценку, зависимости, четыре достижения и таблицу посредством новой миграции на текущей модели. Сохранить оптимизации опубликованной админки. Не включать неопубликованные изменения тарифов, лимитов, счётчиков reading_usage и прочую работу основного дерева.

## QA / verification

- Frontend: npm run type-check, npm test, npm run build, релевантные Playwright admin/profile/retired-link и analytics тесты.
- Backend: dotnet test backend/FutureViewer.slnx, включая PostgreSQL/Testcontainers и миграцию удаления отзывов.
- Rendered browser QA через in-app browser: админка без отзывов, профиль и старые ссылки. Синтетическая авторизация при отсутствии реальной админской сессии; точные mobile viewport покрывает Playwright.
- Для Метрики проверить настоящий tag.js, перехватывая аналитические запросы тестов до отправки, чтобы не создавать ложные конверсии.

## Deploy

- Ветка codex/retention-metrika-feedback, цель origin/main. После QA commit и push проверенной версии в origin/main без force.
- Сервер deploy@80.87.104.245, checkout /opt/future-viewer, env /opt/fv-app/.env.production. До изменения записать текущие SHA/образы, сделать защищённую резервную копию БД для миграции, сохранить конфигурацию и соседние контейнеры.
- Собрать и доставить backend/frontend образы с тегом SHA и действующими публичными build args; обновить checkout fast-forward.
- Команда обновления: sudo -n env FV_IMAGE_TAG=<SHA> FV_SHARED_EDGE_PORT=18080 FV_LEGACY_EDGE_IP=<проверенный-IP-Caddy> docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-deps --no-build --pull never backend frontend.
- Проверить docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps, GET https://alex-taro.ru/health, применённую миграцию, закрытые API отзывов и production browser smoke.
- Если соединение с production остаётся недоступно, явно сообщить, что публикация не выполнена; не выдавать локальное удаление за исправление сайта.


## QA results and publication status

- Backend: 448/448 tests passed (10 Domain, 181 DomainServices, 257 PostgreSQL integration), including migration preservation and retired endpoint tests.
- Frontend: type-check and production build passed; 350/350 unit tests passed. Desktop/mobile admin and profile checks covered removal and legacy redirects.
- In-app browser, synthetic local data: legacy admin feedback URL redirects to clean /admin/readings; only messages/users/stats tabs remain; user drawer and profile contain no feedback or score sections. No real account changes.
- Technical compliance check and git diff --check passed. Existing documentary metadata warnings remain.
- Real production Yandex tag emitted expected pageview, registration_started and email_verified requests in browser tests with all analytics transmissions intercepted locally before sending; no production test accounts or conversions created. Runtime tracking code remains unchanged. Analytics e2e fixtures were updated for the existing required-question and restored-result flows.
- Official Yandex notice dated 16 October confirms delayed reports and limited Direct attribution from 11 October: https://b2b.yandex.ru/adv/news/dostup-k-kabinetu-metrika-i-appmetrica-vosstanavlivaetsya . Private analytics figures are reported in the chat only.
- Production HTTPS /health reports database ready. SSH reaches TCP port 22 but receives no SSH server banner, including with a 45-second timeout. No production database, environment, checkout or containers were changed.
- If SSH remains unavailable after QA, publish the isolated release branch and a draft PR only; defer merge, pre-migration backup and deploy until access is restored.
- Analytics QA: 26 unit tests and all 10 desktop/mobile browser scenarios passed (8 in the serial suite, 2 rerun after correcting stale test expectations). Admin/profile: 44 scenarios covered, with the two affected timing checks rerun successfully after fixture stabilization.
- Independent final review found no release blockers. Before later publishing the original checkout's older untracked `SeparateReadingUsageAndRemoveFeedback` migration, reconcile its duplicate feedback-table drop with this release's migration; it is not part of this branch.
