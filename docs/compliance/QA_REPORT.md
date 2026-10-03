# QA-отчёт compliance remediation

Обновление 3 октября 2026: Telegram удалён из UI, API, фоновых служб и конфигурации. Регистрация теперь содержит один отдельный checkbox ПД; оферта/политика/18+ явно подтверждаются кнопкой «Создать», необязательные согласия выключены. Описания прежнего интерфейса и проверок ниже относятся к предыдущему состоянию. Актуальная проверка: `plans/guest-single-card-preview.md`.

## Повторная проверка — 25 сентября 2026

Текущая рабочая копия, без push/merge/deploy. Результаты ниже заменяют прежнее ограничение об отсутствии Docker. Сам аудит и оставшиеся условия запуска: [RF_AUDIT_2026-09-25.md](RF_AUDIT_2026-09-25.md).

| Проверка | Результат |
|---|---|
| `dotnet test backend/FutureViewer.slnx --no-restore` | PASS: Domain 10, DomainServices 146, Integration 147; всего **303**, 0 failed / 0 skipped; реальный PostgreSQL через Testcontainers |
| EF `has-pending-model-changes --no-build` | PASS: расхождений нет; есть предупреждение о database default/sentinel DeckType, не ошибка модели |
| Frontend `npm run type-check` | PASS |
| Frontend `npm test` | PASS: **202** теста, 32 файла |
| Frontend `npm run build` | PASS, включая генерацию SEO |
| Frontend `npm run test:e2e` | PASS: **10/10** Chromium desktop/mobile; повторено после окончательных изменений |
| `node --test scripts/tests/*.test.mjs` | PASS: **10/10**, включая подмену файла, пропуск нового материала, путь вне репозитория, дубликаты и иностранную основную БД |
| `npm audit` | PASS: **0** уязвимостей после совместимых обновлений; до исправления было 7 (2 high) |
| NuGet Host `package --vulnerable --include-transitive --no-restore --format json` | PASS: **0** записей advisories |
| Workflow YAML, Node syntax, `git diff --check` | PASS |
| Compliance `--scope=backend` | PASS |
| Compliance `--scope=frontend` | BLOCKED: 8 неподтверждённых групп материалов |
| Compliance `--scope=release` | BLOCKED: 78 незакрытых пунктов реестров/доказательств; это не 78 новых программных ошибок |

### Отрисованный интерфейс

Через in-app browser проверены desktop и мобильный экран **390×844**: форма регистрации с 8 выключенными исходно флажками, отдельная ссылка на согласие ПД, недоступная регистрация без опубликованных документов, все 8 юридических маршрутов, отсутствие горизонтального переполнения и `noindex` заблокированных страниц. Визуально проверены настройки хранения: три необязательные категории выключены, выбор необходимых доступен. Синтетический вопрос с `test@example.invalid` отклонён локально с понятным сообщением. Проверена страница «О сервисе».

Ручная браузерная проверка выполнена без авторизованного пользователя и внешних провайдеров. Авторизованные экспорт, отзыв, удаление, повторная аутентификация и оплата проверены компонентными/интеграционными тестами на синтетических данных, не реальной банковской операцией. E2E используют контролируемые ответы API; отсутствие нежелательных внешних запросов в этих сценариях не удостоверяет сетевые потоки production.

Интеграционный прогон обнаружил и проверил устранение ошибки построения DELETE-маршрутов, скрытых записей истории, отказа после сбоя удаления, обхода email verification, подмены proxy headers и конфликтов нескольких акцептов оферты. Новый индекс миграции сохраняет историю акцептов; ограничения его отката описаны в отчёте аудита.

### Непроверенные области

Production infrastructure/health, реальная отправка почты/Telegram/ИИ, реальные чеки/возвраты, договоры/уведомления и юридическое достаточное содержание документов не подтверждались. SAST/CodeQL, Trivy, секрет-скан всей истории и боевое восстановление backup в этом повторном прогоне не выполнялись. Зелёные тесты не снимают блокировку выпуска и не являются юридическим заключением.

## Исторический результат — 1 августа 2026

Дата: 1 августа 2026 года. Проверки выполнены локально; production deployment не выполнялся. Этот отчёт фиксирует результат тестов, а не юридическое заключение и не подтверждение production infrastructure.

## Автоматизированные проверки

| Проверка | Результат |
|---|---|
| `dotnet build backend/FutureViewer.slnx --no-restore` | PASS, 0 warnings / 0 errors |
| Domain tests | PASS, 10/10 |
| DomainServices tests | PASS, 113/113 |
| Изолированные integration tests для YooKassa, YooMoney, processor guard и fragment email links | PASS, 12/12 |
| Полный Testcontainers integration suite | NOT RUN: Docker daemon недоступен (`orbstack` socket отсутствует) |
| EF `has-pending-model-changes` | PASS, model соответствует трём миграциям |
| EF idempotent migration SQL | PASS, сформирован локально во временный файл; EF tool 10.0.6 выдал известное предупреждение о runtime 10.0.10 |
| Frontend `npm run type-check` | PASS |
| Frontend Vitest | PASS, 31 files / 189 tests |
| Frontend Playwright E2E | PASS, 10/10: Chromium desktop + mobile |
| Frontend production build | PASS |
| `npm audit --audit-level=high` | PASS, 0 vulnerabilities |
| NuGet vulnerable transitive package scan | PASS, найденных уязвимых пакетов нет |
| Production Compose render | PASS с example env и отдельным project name |
| JSON, Node syntax, workflow YAML, `git diff --check` | PASS |
| Regression отдельного обязательного `PersonalDataProcessingConsent` | PASS: backend validator/service и frontend unit/E2E проверяют отдельный initial-off флаг, точную ссылку на документ, блокировку регистрации без выбора и самостоятельную versioned consent record |

## Browser / E2E coverage

- Регистрация: четыре самостоятельных mandatory-флага, все initial false — оферта, ознакомление с privacy policy, отдельное согласие на обработку ПД со ссылкой `/legal/personal-data-consent`, 18+ — и четыре самостоятельных optional-флага, также initial false. Unit и Playwright desktop/mobile подтверждают boundary; publication/submit остаются fail closed без утверждённых legal facts.
- Cookies/browser storage: optional categories выключены; `Принять необходимые` сохраняет versioned necessary-only choice; внешние analytics/advertising requests не обнаружены.
- Вопрос с email блокируется локально, `/api/readings` не вызывается.
- Сохранение истории по умолчанию выключено; точный AI/Таро disclaimer присутствует.
- Все восемь `/legal/*` routes открываются на desktop/mobile, не содержат запрещённых placeholders и остаются `noindex` при неподтверждённых facts.
- Дополнительно через in-app browser проверены desktop 1440×900 и mobile 390×844: cookie detail UI, registration, local question block, отсутствие horizontal overflow и console errors.

Authenticated privacy-center mutations и PostgreSQL cascade не проверялись настоящим browser session из-за отсутствующего Docker/Postgres. Они покрыты unit/component tests и добавленными Testcontainers integration tests, которые обязательно запустить в CI/staging.

## Compliance gates

- `--scope=backend`: PASS.
- `--scope=frontend`: ожидаемый FAIL, 8 неподтверждённых shipped IP asset groups.
- `--scope=release`: ожидаемый FAIL, 78 блокеров: operator/legal/service facts, retention, IP, production/localization/processors, backup, support, NPD и инфраструктурные получатели не подтверждены.

Failing release gate является требуемым безопасным результатом. Его нельзя обходить до выполнения `MANUAL_ACTIONS.md` и прикрепления evidence references.
