# Future Viewer

Онлайн ТАРО-расклад с AI-интерпретацией от OpenAI GPT.

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

# 2. Поднять всё через docker-compose
cd ../../..
docker compose up --build
```

- API:      http://localhost:5000
- Swagger:  http://localhost:5000/swagger
- Frontend: http://localhost:5173

## Тесты

```bash
dotnet test backend/FutureViewer.slnx
```

Integration-тесты сами поднимают Postgres через Testcontainers — нужен запущенный Docker daemon.

## План реализации

См. [`plans/purring-jumping-hellman.md`](plans/purring-jumping-hellman.md).
