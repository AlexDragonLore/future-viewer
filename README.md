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

# Контакт саппорта в футере (по умолчанию — support@vualgryaduschego.ru):
dotnet user-secrets set "Support:Email" "support@example.com"

# 2. Поднять всё через docker-compose
cd ../../..
docker compose up --build
```

- API:      http://localhost:5050
- Swagger:  http://localhost:5050/swagger
- Frontend: http://localhost:5173

> Важно: если реальный `OPENAI_API_KEY` когда-либо попадал в локальный `.env`,
> shell history, логи или чат, считайте его скомпрометированным и перевыпустите
> ключ в кабинете OpenAI. Реальные секреты не должны коммититься.

## Production: один сервер

Production-запуск рассчитан на чистый Ubuntu/Debian-сервер с Docker,
Docker Compose и доменом. До запуска убедитесь, что A-запись домена указывает на
IP сервера: Caddy автоматически получит HTTPS-сертификат для этого домена.

```bash
apt update && apt install docker.io docker-compose git -y
mkdir -p /opt/fv-app
git clone <repo-url> /opt/fv-app/current
cd /opt/fv-app/current
bash init.sh
```

`init.sh`:

- создает `/opt/fv-app/.env.production` с правами `600`, если файла еще нет;
- спрашивает домен, AI provider, provider API key, email админа, support email и опциональные
  Telegram/SMTP/YooKassa настройки;
- генерирует `JWT_SECRET` и пароль Postgres;
- запускает `docker-compose.prod.yml` с Postgres, backend, production frontend и
  Caddy reverse proxy на `80/443`.

Production-секреты живут только в `/opt/fv-app/.env.production`. В репозитории
есть только шаблон [`.env.production.example`](.env.production.example).

Обновление уже поднятого сервера:

```bash
cd /opt/fv-app/current
git pull
bash init.sh
```

Полезные команды диагностики:

```bash
docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps
docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer logs -f backend
curl -I https://<your-domain>/health
```

## Тесты

```bash
dotnet test backend/FutureViewer.slnx
```

Integration-тесты сами поднимают Postgres через Testcontainers — нужен запущенный Docker daemon.

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
