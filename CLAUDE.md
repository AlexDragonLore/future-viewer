# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Rules

### Plans
Every finalized /plan must be copied into `plans/` in the repo root:
- Use a descriptive kebab-case filename reflecting the plan topic (e.g. `streaming-ai-parallel-animation.md`), not the auto-generated Claude plan filename.
- Copy the plan right after ExitPlanMode is approved, before starting implementation.

## Architecture

Future Viewer is a tarot-reading web app with AI-generated interpretations. Two deployable units talk over HTTP + JWT: a .NET 10 backend and a Vue 3 frontend, with PostgreSQL as the store and OpenAI (gpt-4o) as the interpreter.

### Backend — Clean Architecture (`backend/src/`)
Four projects with a one-way dependency chain: `Host` → `Infrastructure` → `DomainServices` → `Domain`.

- **`FutureViewer.Domain`** — entities (`Reading`, `ReadingCard`, `TarotCard`, `User`, `Spread`), enums, value objects. No dependencies on the rest.
- **`FutureViewer.DomainServices`** — use cases (`ReadingService`, `InterpretationService`, `CardDeckService`, `AuthService`), DTOs, FluentValidation validators, and **interfaces for infrastructure** (e.g. `IReadingRepository`, the AI interpreter port). Knows nothing about EF Core, OpenAI, or HTTP.
- **`FutureViewer.Infrastructure`** — EF Core `AppDbContext` + migrations + repositories, `TarotDeckSeed`, JWT token generator, and the OpenAI-backed interpreter implementation. Implements the interfaces declared in DomainServices.
- **`FutureViewer.Host`** — ASP.NET Core Minimal API entry point. `Program.cs` wires DI via `AddDomainServices()` + `AddInfrastructure(config)`, configures JWT bearer auth, CORS, OpenAPI, the global `ExceptionHandlerMiddleware`, and maps endpoints in `Endpoints/` (`MapReadings`, `MapAuth`, `MapCards`). At startup (outside `Testing` env) it runs EF migrations and seeds the tarot deck via `DatabaseInitializer`.

The central flow: `POST /api/readings` → `ReadingService.CreateAsync` → draw from `CardDeckService` → `InterpretationService` (calls `OpenAIInterpreter`) → persist via repository → return `ReadingResult`.

Configuration is loaded via `IConfiguration` sections (`Jwt`, `OpenAI`, `ConnectionStrings:Default`, `Cors:AllowedOrigins`). Secrets in dev live in user-secrets of `FutureViewer.Host`; in docker-compose they come from env vars `JWT_SECRET` and `OPENAI_API_KEY`.

### Frontend (`frontend/src/`)
Vue 3 + TS + Vite 6 SPA. Pinia for state, Vue Router for navigation, Tailwind for styling, **GSAP** for card animations, **marked** for rendering AI markdown, **axios** for HTTP. JWT is stored in `localStorage` as `fv_token` and attached by `httpClient.ts`.

User journey is a 3-view pipeline mirrored by Pinia state in `stores/useReadingStore.ts`:
`HomeView` (pick spread + question) → `ReadingView` (shuffle → deal → flip, driven by `composables/useCardAnimation.ts` and `composables/useSpread.ts`) → `ResultView` (interpretation + per-card meanings).

Procedural sound effects live in `composables/useAudio.ts` (Web Audio, no assets).

### Integration tests
`backend/tests/FutureViewer.Integration.Tests` uses `WebApplicationFactory` with `ASPNETCORE_ENVIRONMENT=Testing` (which disables the startup migration/seed) and spins up Postgres via **Testcontainers** — a running Docker daemon is required.

## Common Commands

### Backend
```bash
# Run the whole solution's tests (requires Docker for integration tests)
dotnet test backend/FutureViewer.slnx

# Run a single test project
dotnet test backend/tests/FutureViewer.DomainServices.Tests/FutureViewer.DomainServices.Tests.csproj

# Run a single test by name
dotnet test backend/FutureViewer.slnx --filter "FullyQualifiedName~ReadingServiceTests.CreateAsync_ReturnsResult"

# Run the API locally (from repo root)
dotnet run --project backend/src/FutureViewer.Host

# Set dev secrets (run inside backend/src/FutureViewer.Host)
dotnet user-secrets set "OpenAI:ApiKey" "sk-..."
dotnet user-secrets set "Jwt:Secret" "$(openssl rand -base64 48)"

# EF Core migrations (run inside backend/src/FutureViewer.Host)
dotnet ef migrations add <Name> --project ../FutureViewer.Infrastructure
dotnet ef database update --project ../FutureViewer.Infrastructure
```

### Frontend (`cd frontend`)
```bash
npm run dev          # Vite dev server on :5173
npm run build        # vue-tsc --noEmit && vite build
npm run type-check   # type-only check
npm test             # vitest run (happy-dom)
npm run test:watch
npx vitest run tests/components/SomeComponent.spec.ts   # single test file
npx vitest run -t "renders the spread"                  # single test by name
```

### Full-stack via Docker
```bash
docker compose up --build
# API :5050 (host) → :8080 (container), Postgres :5432, Frontend :5173
```
