# Future Viewer / Вуаль Грядущего

Онлайн ТАРО-расклад с AI-интерпретацией через OpenAI-compatible провайдера (OpenAI/ChatGPT или DeepSeek). «Future Viewer» — внутреннее имя репозитория и неймспейсов; пользовательское название — «Вуаль Грядущего».

## Стек

- **Backend**: .NET 10, ASP.NET Core Minimal API, EF Core + Npgsql, PostgreSQL 17
- **Frontend**: Vue 3 + TypeScript + Vite, Pinia, GSAP, Tailwind CSS
- **AI**: OpenAI/ChatGPT или DeepSeek
- **Tests**: xUnit + FluentAssertions + Moq, Testcontainers.PostgreSQL
- **CI**: GitHub Actions

## Структура

```
future-viewer/
├── backend/          # .NET 10 solution (Domain, DomainServices, Infrastructure, Host)
├── frontend/         # Vue 3 + TS + Vite
├── plans/            # архитектурные планы
├── .github/          # CI workflows
└── docker-compose.yml
```

## Локальный запуск

Локальный `docker-compose.yml` предназначен для разработки: backend работает в
`Development`, frontend запускается через Vite dev server, Postgres открыт на
`localhost:5432`.

```bash
# 1. Секреты бэкенда
cd backend/src/FutureViewer.Host
dotnet user-secrets set "AI:Provider" "OpenAI"       # или "DeepSeek"
dotnet user-secrets set "AI:Enabled" "true"          # только после test registry approval
dotnet user-secrets set "OpenAI:ApiKey" "sk-..."     # если AI:Provider=OpenAI
dotnet user-secrets set "DeepSeek:ApiKey" "<key>"    # если AI:Provider=DeepSeek
dotnet user-secrets set "Jwt:Secret" "$(openssl rand -base64 48)"

# Опционально — SMTP для подтверждения email и восстановления пароля.
# Если Email:Host пуст, письма пишутся в лог (dev fallback) — токен из лога
# можно вставить в URL вручную.
dotnet user-secrets set "Email:Host" "smtp.yandex.ru"
dotnet user-secrets set "Email:Port" "465"
dotnet user-secrets set "Email:Username" "no-reply@example.com"
dotnet user-secrets set "Email:Password" "..."
dotnet user-secrets set "Email:From" "no-reply@example.com"
dotnet user-secrets set "Email:UseSsl" "true"
dotnet user-secrets set "Email:FrontendUrl" "http://localhost:5173"

# Подтверждённый контакт саппорта в футере (без repository default):
dotnet user-secrets set "Support:Email" "support@example.com"

# 2. Поднять всё через docker-compose
cd ../../..
docker compose up --build
```

- API:      http://localhost:5050
- Swagger:  http://localhost:5050/swagger
- Frontend: http://localhost:5173

### Локальный smoke-test оплаты

Для проверки платежного сценария не коммитьте credentials. Передавайте ключи
только через env/runtime config.

#### ЮKassa Payments API

Для ЮKassa Payments API нужны именно credentials магазина:
`Yukassa__ShopId` и `Yukassa__SecretKey`. Payout/gate credentials для этого
endpoint не подходят и дают `401 invalid_credentials` с описанием
`Authentication type is not allowed`.

```bash
docker compose up -d postgres

Payment__Provider=Yukassa \
Payment__Enabled=true \
Payment__WebhookEnabled=true \
Yukassa__ShopId=<test-shop-id> \
Yukassa__SecretKey=<test-secret-key> \
Yukassa__ReturnUrl=http://localhost:5174/payment/success \
ASPNETCORE_URLS=http://localhost:5050 \
ConnectionStrings__Default='Host=localhost;Port=5432;Database=future_viewer;Username=future_viewer;Password=future_viewer_dev' \
Cors__AllowedOrigins__0=http://localhost:5174 \
dotnet run --project backend/src/FutureViewer.Host --no-launch-profile

cd frontend
VITE_API_URL=http://localhost:5050 npm run dev -- --host 127.0.0.1 --port 5174 --strictPort
```

Открывайте именно `http://localhost:5174/`, а не `127.0.0.1`, если backend
разрешает CORS только для `http://localhost:5174`. Затем зарегистрируйте
тестового пользователя, нажмите `Оплатить доступ` и проверьте редирект на
confirmation URL ЮKassa. В чисто локальном окружении внешний webhook ЮKassa не
сможет достучаться до `localhost`; для полной проверки активации нужен публичный
tunnel/webhook URL или ручной локальный `POST /api/payments/webhook` после
успешной тестовой оплаты.

#### YooMoney redirect

YooMoney redirect flow не вызывает Payments API ЮKassa. Backend создает URL
`https://yoomoney.ru/quickpay/confirm?...`, frontend переводит пользователя на
страницу YooMoney, а подписка активируется только после HTTP-уведомления от
YooMoney на `/api/payments/webhook`.

Для локального запуска:

```bash
docker compose up -d postgres

Payment__Provider=YooMoney \
Payment__Enabled=true \
Payment__WebhookEnabled=true \
YooMoney__Receiver=<yoomoney-wallet-number> \
YooMoney__NotificationSecret=<http-notification-secret> \
YooMoney__ReturnUrl=http://localhost:5174/payment/success \
ASPNETCORE_URLS=http://localhost:5050 \
ConnectionStrings__Default='Host=localhost;Port=5432;Database=future_viewer;Username=future_viewer;Password=future_viewer_dev' \
Cors__AllowedOrigins__0=http://localhost:5174 \
dotnet run --project backend/src/FutureViewer.Host --no-launch-profile

cd frontend
VITE_API_URL=http://localhost:5050 npm run dev -- --host 127.0.0.1 --port 5174 --strictPort
```

`YooMoney__Receiver` — это номер кошелька ЮMoney, на который зачисляются
деньги. Это не `gate_id` выплат ЮKassa. `YooMoney__NotificationSecret` должен
совпадать с секретом HTTP-уведомлений в настройках кошелька.

Для локальных вебхуков YooMoney нужен публичный HTTPS tunnel до backend, потому
что `localhost` недоступен провайдеру. URL уведомлений в настройках YooMoney:

- локально через tunnel: `https://<tunnel-host>/api/payments/webhook`;
- production: `https://alex-taro.ru/api/payments/webhook`.

Для production success redirect используйте `https://alex-taro.ru/payment/success`.
YooMoney отправляет уведомление методом `POST` с
`Content-Type: application/x-www-form-urlencoded`; app принимает кодом `200 OK`.
Для webhook smoke используйте только официальный test fixture/способ проверки,
соответствующий фактически заключённому договору и актуальной документации
провайдера. Самодельный алгоритм подписи или ручная активация по redirect не
допускаются.

> Важно: если реальный `OPENAI_API_KEY` когда-либо попадал в локальный `.env`,
> shell history, логи или чат, считайте его скомпрометированным и перевыпустите
> ключ в кабинете OpenAI. Реальные секреты не должны коммититься.

## Production

Доступность интеграций определяется фактической конфигурацией: официальный HTTPS endpoint и ключ выбранного AI-провайдера, рабочий почтовый транспорт, credentials платёжного провайдера и включённая обработка вебхуков. Явный operational flag `false` по-прежнему отключает соответствующую функцию. Неизвестные документарные сведения остаются незаполненными и не блокируют регистрацию, расклады или оплату.

Почта production уже настроена через существующий ящик REG.RU и HTTPS transport `RegruWebmail`; пересоздавать ящик или заменять его credentials не требуется. Пустые GitHub Secrets не перезаписывают рабочие значения в серверном env. Итоговая конфигурация проверяется после объединения с серверными значениями, до замены env и перезапуска контейнеров. Обработка оплаты по-прежнему требует подтверждённого вебхука; success redirect сам по себе доступ не активирует.

На общем сервере application Compose stack не владеет публичными `80/443`: host-level shared edge маршрутизирует exact hostname `alex-taro.ru` на отдельный loopback port Future Viewer. Контейнеры, БД/user, networks, volumes, env, secrets, logs и backups должны быть отделены от janetka.ru.

Канонические инструкции:

- [deployment runbook](docs/compliance/DEPLOYMENT_RUNBOOK.md);
- [rollback runbook](docs/compliance/ROLLBACK_RUNBOOK.md);
- [shared-host isolation](docs/compliance/SHARED_HOST_ISOLATION.md);
- [backup/restore](docs/compliance/BACKUP_RESTORE_RUNBOOK.md);
- [manual actions](docs/compliance/MANUAL_ACTIONS.md).
- [REG.RU HTTPS email transport](deploy/regru-webmail.md);
- [настройка Яндекс Директа и подсчёт лидов](docs/yandex-direct-setup.md).

Workflow запускается только для `main`, требует backend/frontend/security/release-compliance checks и protected GitHub environment. Он использует один pinned-host-key, non-root deploy credential, exact commit SHA, project-scoped Compose и до изменения сохраняет предыдущие Git revision, image tag и env для rollback. Отсутствующая конфигурация завершает job ошибкой; host-wide Docker prune запрещён.

Проверка production:

```bash
cd /opt/fv-app
docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps
curl -fsS https://alex-taro.ru/health
```

## Тесты

```bash
dotnet test backend/FutureViewer.slnx
cd frontend
npm run type-check
npm test
npm run test:e2e
npm run build
```

Backend integration-тесты сами поднимают Postgres через Testcontainers — нужен запущенный Docker daemon. Playwright E2E автоматически запускают локальный Vite и проверяют desktop/mobile Chromium; перед первым локальным запуском выполните `npx playwright install chromium`.

## Планы реализации

См. директорию [`plans/`](plans/) — каждый план соответствует серии PR.

## Лицензия

Проект распространяется по custom source-available лицензии (см. [`LICENSE`](LICENSE)).

TL;DR:

- Изучать, форкать и запускать локально для личного/учебного/исследовательского использования — **можно бесплатно**.
- Любое **коммерческое использование** (продажа, SaaS, интеграция в платный продукт, получение выручки внутри организации) требует отдельного письменного соглашения с автором.
- Типовые условия соглашения — **20% от валовой выручки (gross revenue)** в пользу автора ежеквартально.
- Контакт для коммерческих запросов: **utre43@gmail.com**.

Текст лицензии — на русском и английском; при расхождении приоритет у русской версии. Документ не является готовым юридическим инструментом и до реального использования в спорах должен быть проверен юристом.

## License

The project is distributed under a custom source-available license (see [`LICENSE`](LICENSE)).

TL;DR:

- Studying, forking, and running the code locally for personal, educational, or research purposes is **free of charge**.
- Any **commercial use** (selling, SaaS, integrating into a paid product, deriving revenue within an organization) requires a separate written agreement with the Author.
- Standard terms are **20% of gross revenue** paid to the Author on a quarterly basis.
- Commercial inquiries: **utre43@gmail.com**.

The license text is bilingual (Russian + English); the Russian version prevails in case of conflict. This is not a finalized legal document and should be reviewed by qualified legal counsel before being relied upon in a dispute.
