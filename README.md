# Future Viewer / Вуаль Грядущего

Онлайн ТАРО-расклад с AI-интерпретацией от OpenAI GPT. «Future Viewer» — внутреннее имя репозитория и неймспейсов; пользовательское название — «Вуаль Грядущего».

## Стек

- **Backend**: .NET 10, ASP.NET Core Minimal API, EF Core + Npgsql, PostgreSQL 17
- **Frontend**: Vue 3 + TypeScript + Vite, Pinia, GSAP, Tailwind CSS
- **AI**: OpenAI GPT-4o
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

```bash
# 1. Секреты бэкенда
cd backend/src/FutureViewer.Host
dotnet user-secrets set "OpenAI:ApiKey" "sk-..."
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
