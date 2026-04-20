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

- **`FutureViewer.Domain`** — entities (`Reading`, `ReadingCard`, `TarotCard`, `User`, `Spread`, `ReadingFeedback`, `Achievement`, `UserAchievement`), enums (`FeedbackStatus`, `SubscriptionStatus`), value objects. No dependencies on the rest.
- **`FutureViewer.DomainServices`** — use cases (`ReadingService`, `InterpretationService`, `CardDeckService`, `AuthService`, `SubscriptionService`, `FeedbackService`, `AchievementService`, `LeaderboardService`, `TelegramLinkService`, `AdminService`), DTOs, FluentValidation validators, and **interfaces for infrastructure** (e.g. `IReadingRepository`, `IFeedbackRepository`, `IAchievementRepository`, `ILeaderboardRepository`, the AI interpreter port `IAIInterpreter`, the AI scorer port `IFeedbackScorer`, `ITelegramNotifier`, `ITelegramLinkUrlBuilder`). Knows nothing about EF Core, OpenAI, or HTTP.
- **`FutureViewer.Infrastructure`** — EF Core `AppDbContext` + migrations + repositories, `TarotDeckSeed`, JWT token generator, the OpenAI-backed interpreter (`OpenAIInterpreter`) and feedback scorer (`FeedbackScoringInterpreter`), the Telegram subsystem under `Telegram/` (`TelegramBotClientProvider`, `TelegramBotService : ITelegramNotifier`, `TelegramUpdateHandler` for `/start <token>`, `TelegramPollingHostedService`, `TelegramLinkUrlBuilder`), and `BackgroundServices/FeedbackNotificationJob` — a thin loop that delegates each tick to `FeedbackNotificationProcessor.ProcessBatchAsync` (same code path reused by the admin `POST /api/admin/feedbacks/run-notifications` endpoint). Uses the `Telegram.Bot` SDK. Implements the interfaces declared in DomainServices.
- **`FutureViewer.Host`** — ASP.NET Core Minimal API entry point. `Program.cs` wires DI via `AddDomainServices()` + `AddInfrastructure(config)`, configures JWT bearer auth, registers an `Admin` authorization policy (`RequireRole("Admin")`), sets up CORS, OpenAPI, the global `ExceptionHandlerMiddleware`, and maps endpoints in `Endpoints/` (`MapReadings`, `MapAuth`, `MapCards`, `MapSubscription`, `MapPayments`, `MapFeedbacks`, `MapLeaderboard`, `MapAchievements`, `MapTelegram`, `MapAdmin`). At startup (outside `Testing` env) it runs EF migrations, seeds the tarot deck + achievements catalog, and promotes users whose email matches `Admin:Emails` to `IsAdmin=true` via `DatabaseInitializer`.

The central reading flow: `POST /api/readings` → `ReadingService.CreateAsync` → draw from `CardDeckService` → `InterpretationService` (calls `OpenAIInterpreter`) → persist via repository → `FeedbackService.ScheduleAsync` (best-effort, +24h) → return `ReadingResult`.

The gamification flow: `FeedbackNotificationJob` picks pending feedbacks where `ScheduledAt <= now` and sends the deep link via Telegram (only marks `Notified` on confirmed send) → user opens `/feedback/{token}` (anonymous, token-authenticated) → `POST /api/feedbacks/{token}` → `FeedbackScoringInterpreter` scores sincerity + follow-through → feedback transitions to `Scored`. `AchievementService.CheckAndGrantAsync` runs on `GET /api/achievements/me` and grants any newly-earned codes (unique constraint on `(user_id, achievement_id)`).

The admin panel (`/api/admin/*`, frontend under `/admin`) is gated by `RequireAuthorization("Admin")`. `JwtTokenService` emits a `role=Admin` claim only when `User.IsAdmin=true`; `AuthResponse.IsAdmin` mirrors the flag to the frontend. Admin toggles take effect on next login (JWT staleness accepted — this is a debug tool). `AdminService` orchestrates all admin mutations; each mutation logs at `Information` with `actorUserId`, `actorEmail`, `action`, `targetId`. Admin bootstrap: the `Admin:Emails` config array is consulted on every startup (via `DatabaseInitializer.SeedAdminsAsync`) — existing users matching any listed email (case-insensitive) get promoted to `IsAdmin=true`; unknown emails are silently skipped.

Configuration is loaded via `IConfiguration` sections (`Jwt`, `OpenAI`, `Telegram`, `Admin`, `ConnectionStrings:Default`, `Cors:AllowedOrigins`). The Telegram subsystem no-ops when `Telegram:BotToken` is empty; `FeedbackNotificationJob` and `TelegramPollingHostedService` exit early in that case. Telegram updates are consumed via long polling only — `TelegramPollingHostedService` calls `DeleteWebhook` on startup so any pre-existing webhook is cleared before `ReceiveAsync` begins. Secrets in dev live in user-secrets of `FutureViewer.Host`; in docker-compose they come from env vars `JWT_SECRET`, `OPENAI_API_KEY`, `TELEGRAM_BOT_TOKEN`, `TELEGRAM_BOT_USERNAME`, `TELEGRAM_SITE_URL`.

### Frontend (`frontend/src/`)
Vue 3 + TS + Vite 6 SPA. Pinia for state, Vue Router for navigation, Tailwind for styling, **GSAP** for card animations, **marked** for rendering AI markdown, **axios** for HTTP. JWT is stored in `localStorage` as `fv_token` and attached by `httpClient.ts`.

User journey is a 3-view pipeline mirrored by Pinia state in `stores/useReadingStore.ts`:
`HomeView` (pick spread + question) → `ReadingView` (shuffle → deal → flip, driven by `composables/useCardAnimation.ts` and `composables/useSpread.ts`) → `ResultView` (interpretation + per-card meanings).

Procedural sound effects live in `composables/useAudio.ts` (Web Audio, no assets).

Gamification views: `FeedbackView` (`/feedback/:token`, anonymous — Telegram deep-link lands here), `LeaderboardView` (`/leaderboard`, anonymous), `AchievementsView` (`/achievements`, auth), `ProfileView` (`/profile`, auth) — backed by `useLeaderboardStore`, `useAchievementStore`, `useProfileStore`, and API modules `feedbackApi`, `leaderboardApi`, `achievementApi`, `telegramApi`.

Admin views: `AdminView` (`/admin`) with child routes `admin/users`, `admin/users/:id`, `admin/feedbacks`, `admin/stats` — meta `{ requiresAuth: true, requiresAdmin: true }`; non-admins are redirected to `/` by the router guard. Backed by `useAdminStore` (paginated users + feedbacks, selected-user detail, stats), API module `adminApi`, and types in `types/admin.ts`. The `SiteHeader` nav entry only renders when `useAuthStore.isAdmin === true`. `isAdmin` is mirrored in `localStorage` under `fv_is_admin` alongside `fv_token`.

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
dotnet user-secrets set "Telegram:BotToken" "<token from BotFather>"    # optional — bot features no-op if unset

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
