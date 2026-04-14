# Future Viewer — онлайн ТАРО-расклад с AI-интерпретацией

## Context
Строим приложение онлайн-раскладов карт ТАРО. Пользователь выбирает тип расклада и вопрос, «тянет» карты, получает мистическую интерпретацию от OpenAI GPT. Авторизация опциональная: анонимы могут делать расклады, зарегистрированные — сохранять историю.

Текущее состояние репозитория — скелет .NET 10 solution:
- `backend/FutureViewer.slnx` (VS slnx-формат) с 4 проектами в `src/` и 3 в `tests/`
- все проекты содержат только `Class1.cs` заглушки
- `FutureViewer.Host` — дефолтный ASP.NET Core Minimal API с `WeatherForecast`
- `FutureViewer.Host.csproj` уже ссылается на Domain, DomainServices, Infrastructure
- `TargetFramework = net10.0`, nullable + implicit usings включены
- `frontend/` ещё нет, CI/Docker/`.gitignore` — тоже

Цель плана — превратить этот скелет в работающее MVP: бэкенд, фронт, БД, CI, локальный Docker, разделение окружений и секретов.

---

## Репозиторий (целевая структура)

```
future-viewer/
├── backend/
│   ├── FutureViewer.slnx
│   ├── src/
│   │   ├── FutureViewer.Domain/
│   │   ├── FutureViewer.DomainServices/
│   │   ├── FutureViewer.Infrastructure/
│   │   └── FutureViewer.Host/
│   ├── tests/
│   │   ├── FutureViewer.Domain.Tests/
│   │   ├── FutureViewer.DomainServices.Tests/
│   │   └── FutureViewer.Integration.Tests/
│   └── Dockerfile
├── frontend/                       # создаём с нуля (Vue 3 + TS + Vite)
│   └── Dockerfile
├── plans/                          # планы хранятся в репо
│   └── purring-jumping-hellman.md  # ← этот план
├── .github/workflows/
│   ├── backend.yml
│   ├── frontend.yml
│   ├── deploy-staging.yml
│   └── deploy-production.yml
├── docker-compose.yml
└── .gitignore
```

---

## Backend — 4 сборки

### 1. `FutureViewer.Domain`
Чистые доменные сущности, без внешних зависимостей. Удаляем `Class1.cs`.
- `Entities/`: `TarotCard`, `Spread`, `Reading`, `ReadingCard`, `User`
- `Enums/`: `CardSuit`, `SpreadType`, `CardOrientation` (upright/reversed)
- `ValueObjects/`: `CardPosition`, `InterpretationResult`
- `Events/`: `ReadingCreatedEvent`, `ReadingCompletedEvent`

### 2. `FutureViewer.DomainServices`
Бизнес-логика и интерфейсы портов. Зависит только от Domain. Без MediatR — обычные сервис-классы, регистрируются в DI напрямую.
- `Interfaces/`: `IReadingRepository`, `IUserRepository`, `IAIInterpreter`, `ICardDeck`
- `Services/`:
  - `ReadingService` — `CreateReadingAsync(CreateReadingRequest)`, `GetReadingAsync(id)`, `GetHistoryAsync(userId)`
  - `CardDeckService` — тасование, выбор N карт, определение ориентации
  - `InterpretationService` — собирает промпт, дёргает `IAIInterpreter`, возвращает текст
  - `AuthService` — регистрация/логин, использует `IPasswordHasher` + `IJwtTokenService`
- `DTOs/`: `CreateReadingRequest`, `ReadingResult`, `InterpretationResponse`, `RegisterRequest`, `LoginRequest`, `AuthResponse`

### 3. `FutureViewer.Infrastructure`
Реализация портов. Зависит от Domain + DomainServices.
- `Persistence/`:
  - `AppDbContext` (EF Core 10 + Npgsql)
  - `Repositories/`: `ReadingRepository`, `UserRepository`
  - `Configurations/` — EF Fluent API
  - `Migrations/`
- `AI/`:
  - `OpenAIInterpreter : IAIInterpreter` — клиент к GPT-4o
  - Промпт: карты + позиции + ориентация + вопрос → мистическая интерпретация
- `Auth/`: `JwtTokenService`, `PasswordHasher`
- `DependencyInjection/`: `InfrastructureServiceExtensions.AddInfrastructure(...)`

### 4. `FutureViewer.Host`
ASP.NET Core 10 Minimal API. Вычищаем шаблонный `WeatherForecast`.
- `Endpoints/`: `ReadingEndpoints`, `AuthEndpoints`, `CardEndpoints` (extension-методы `MapReadings` и т.п.)
- `Middleware/ExceptionHandlerMiddleware`
- `Program.cs`: DI (регистрация сервисов из DomainServices и Infrastructure), Swagger, CORS для Vue-фронта, JWT-auth, запуск миграций при старте
- `Configuration/`: биндинг секций `Jwt`, `OpenAI`, `ConnectionStrings`

---

## Тесты

### `FutureViewer.Domain.Tests`, `FutureViewer.DomainServices.Tests`
- xUnit + FluentAssertions + Moq
- Только бизнес-логика, без инфраструктуры

### `FutureViewer.Integration.Tests`
- xUnit + `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory<Program>`)
- `Testcontainers.PostgreSQL` — поднимает реальный Postgres в Docker на каждый прогон
- `IAIInterpreter` подменяем на stub, чтобы не ходить в OpenAI из CI
- Структура:
  ```
  tests/FutureViewer.Integration.Tests/
  ├── Fixtures/IntegrationTestFixture.cs   # WebApplicationFactory + Testcontainers
  ├── Helpers/DatabaseHelper.cs             # reset/seed между тестами
  └── Tests/
      ├── ReadingsEndpointTests.cs
      ├── AuthEndpointTests.cs
      └── HistoryEndpointTests.cs
  ```
- Сценарии:
  - `POST /api/readings` → создаёт запись в БД, возвращает карты + интерпретацию от stub'а
  - `POST /api/auth/register` + `POST /api/auth/login` → выдаётся валидный JWT
  - `GET /api/readings/history` с JWT → возвращает пользовательские расклады

---

## Frontend — Vue 3 + TypeScript + Vite

Инициализируем через `npm create vite@latest frontend -- --template vue-ts`.
Stack: **Vue 3 + TS + Pinia + Vue Router + GSAP** (анимация) + **@vueuse/core** + **Tailwind CSS** для стилей.

### Визуальный стиль

- **Тема**: тёмная мистическая эстетика — глубокий тёмно-фиолетовый/индиго фон (`#0b0618 → #1a0a2e`), градиенты, золотые/янтарные акценты (`#f5c26b`), серебристая типографика
- **Фон**: полноэкранный градиент + ambient-слои:
  - слой звёзд/частиц (canvas, `particles.vue3` или свой `Starfield.vue` на `requestAnimationFrame`)
  - дымчатые туманные облака (SVG blobs с `filter: blur()` и медленным `translate` через GSAP)
  - тонкий vignette overlay по краям
- **Типографика**: заголовки — `Cinzel` / `Marcellus`, основной текст — `Inter`; мистические капсы, letter-spacing на заголовках
- **Поверхности**: «карточки» UI — `backdrop-filter: blur(12px)` + полупрозрачный фиолет + золотая border 1px + мягкое свечение (`box-shadow: 0 0 40px rgba(245,194,107,.15)`)
- **Курсор на колоде**: сменный, с лёгким glow-trail

### Анимация карт (ключевая часть)

Всё строится на **GSAP** (`gsap`, `@gsap/vue`) + `FLIP` подход через `gsap.timeline()`.

**Этапы расклада:**

1. **Появление колоды**: стопка из 78 рубашек в центре. Колода «дышит» (`y: ±4px, scale: 1 ± 0.01`, infinite yoyo).
2. **Тасование**: по клику — карты разлетаются по эллипсу, быстро вращаются вокруг центра (`rotation: 720`), сходятся обратно в стопку. 1.2s. Сопровождается мягким звуком (опционально).
3. **Полёт на позиции** (главный визуальный эффект):
   - Каждая карта из верхушки колоды стартует позицию `(deckX, deckY)`, стартовая `scale: 0.9`, `rotationY: 180` (лицом вниз)
   - Летит по **безье-кривой** (`gsap.to` с `motionPath`) к своему слоту на `SpreadBoard`
   - Во время полёта: `rotation: ±15deg` покачивание, лёгкий `scale: 1.05 → 1` на приземлении
   - Stagger между картами `0.25s`, общая длительность полёта одной карты `0.8s`, ease `power2.inOut`
   - Под каждой картой — золотое свечение-пятно, которое появляется в момент приземления (`opacity 0 → 1`, `scale 0.5 → 1.2`)
4. **Переворот**: после приземления всех карт — по клику или автоматически — поочерёдный flip (`rotationY: 180 → 0`), stagger `0.35s`, длительность `0.6s`. В момент переворота — вспышка золотой свечения вокруг карты.
5. **Раскрытие интерпретации**: текст AI-интерпретации появляется с typewriter-эффектом (посимвольно, `~30ms/символ`), фоновое свечение усиливается.

**Реализация:**
- Композабл `useCardAnimation.ts` инкапсулирует GSAP-timeline'ы: `dealCards(cards, positions)`, `flipCard(cardRef)`, `shuffleDeck()`
- Композабл `useSpread.ts` считает координаты слотов по типу расклада (ThreeCard / CelticCross / и т.п.)
- `CardFlip.vue` — один компонент карты с `:style="{ transform }"` и `ref` для GSAP, двусторонний (front + back), `transform-style: preserve-3d`, `backface-visibility: hidden`
- `SpreadBoard.vue` — SVG/абсолютное позиционирование слотов, получает список позиций от `useSpread`
- `CardDeck.vue` — стопка карт, эмитит события начала шафла/раздачи
- `Starfield.vue` — canvas-фон со звёздами и parallax на движение мыши
- Для 60fps: все анимации через `transform`/`opacity` (GPU), никаких `top/left`; `will-change: transform` на карте

### Структура

```
frontend/src/
├── assets/
│   ├── cards/              # 78 PNG/WEBP рубашка + 78 лицевых
│   ├── fonts/              # Cinzel, Inter (self-hosted)
│   ├── textures/           # шум, туман, дымка
│   └── styles/
│       ├── main.css        # Tailwind base + кастомные CSS-переменные темы
│       └── animations.css  # keyframes для ambient-анимаций
├── components/
│   ├── cards/              # CardFlip.vue, CardDeck.vue, CardReveal.vue
│   ├── spread/             # SpreadBoard.vue, SpreadSelector.vue, PositionLabel.vue
│   ├── reading/            # ReadingResult.vue, InterpretationText.vue (typewriter)
│   ├── fx/                 # Starfield.vue, MistLayer.vue, Glow.vue
│   └── ui/                 # BaseButton.vue (с glow на hover), BaseModal.vue, LoadingSpinner.vue
├── views/
│   ├── HomeView.vue        # hero + выбор расклада + анимированный фон
│   ├── ReadingView.vue     # колода → шафл → полёт → flip
│   ├── ResultView.vue      # результат + typewriter интерпретация
│   ├── HistoryView.vue
│   └── AuthView.vue
├── stores/
│   ├── useReadingStore.ts
│   ├── useAuthStore.ts
│   └── useCardsStore.ts
├── api/
│   ├── httpClient.ts       # axios + JWT interceptor
│   ├── readingApi.ts
│   └── authApi.ts
├── composables/
│   ├── useCardAnimation.ts # все GSAP timeline'ы (deal, flip, shuffle)
│   ├── useSpread.ts        # позиции слотов по типу расклада
│   └── useTypewriter.ts    # посимвольное раскрытие текста
├── router/index.ts
└── types/index.ts
```

### Зависимости фронта (ключевые)

```
vue, vue-router, pinia, axios,
gsap, @vueuse/core, @vueuse/motion,
tailwindcss, postcss, autoprefixer
```

---

## База данных (PostgreSQL 17)

Таблицы:
- `users` — `id`, `email`, `password_hash`, `created_at`
- `tarot_cards` — `id`, `name`, `suit`, `number`, `description_upright`, `description_reversed`, `image_path`
- `readings` — `id`, `user_id` (nullable), `spread_type`, `question`, `created_at`, `ai_interpretation`
- `reading_cards` — `id`, `reading_id`, `card_id`, `position`, `is_reversed`

78 карт сидятся миграцией / `IHostedService` при первом старте.

---

## Окружения и секреты

### Три окружения: Local / Staging / Production

**Backend** — через `ASPNETCORE_ENVIRONMENT`:
```
backend/src/FutureViewer.Host/
├── appsettings.json              # базовая структура, без секретов
├── appsettings.Development.json  # Serilog verbose, Swagger on
├── appsettings.Staging.json
└── appsettings.Production.json   # минимум логов
```

**Frontend** — через Vite `.env`:
```
frontend/
├── .env.local         # gitignored — VITE_API_URL=http://localhost:5000
├── .env.staging       # VITE_API_URL=https://staging.api.example.com
└── .env.production    # VITE_API_URL=https://api.example.com
```

### Где хранить секреты

| Окружение   | Где                              | Механизм                                                              |
|-------------|----------------------------------|-----------------------------------------------------------------------|
| Local       | `dotnet user-secrets`            | `dotnet user-secrets set "OpenAI:ApiKey" "sk-..."` — вне репо         |
| Staging     | GitHub Actions Secrets           | `OPENAI_API_KEY`, `DB_CONNECTION_STRING`, `JWT_SECRET` → env vars     |
| Production  | GitHub Environment `production`  | То же, но с ручным approval на деплой                                 |

Правило: `appsettings.*.json` **не содержат реальных секретов**, только структуру/плейсхолдеры. .NET автоматически оверрайдит их переменными окружения.

`.gitignore` дополнительно:
- `.env.local`, `.env.*.local`
- `*.user`, `*.user.secrets`
- `**/appsettings.*.local.json`
- стандартные `bin/`, `obj/`, `node_modules/`, `dist/`

---

## GitHub CI

### `.github/workflows/backend.yml`
- Trigger: push/PR в `main`, пути `backend/**`
- Steps: checkout → setup-dotnet@v4 (10.x) → `dotnet restore` → `dotnet build` → unit-тесты → integration-тесты
- Integration: `services: postgres:17` в job, `OPENAI_API_KEY` подменяется stub'ом, `DB_CONNECTION_STRING` из service
- `ASPNETCORE_ENVIRONMENT=Testing`

### `.github/workflows/frontend.yml`
- Trigger: push/PR в `main`, пути `frontend/**`
- Steps: checkout → setup-node@v4 (Node 22) → `npm ci` → `tsc --noEmit` → `npm run build -- --mode staging`

### `deploy-staging.yml` / `deploy-production.yml`
- Staging — авто на merge в `main`
- Production — по тегу `v*` через GitHub Environment `production` с manual approval

---

## Docker

`docker-compose.yml` для локальной разработки:
- `postgres:17` с volume `pgdata`
- `backend` — собирается из `backend/Dockerfile`
- `frontend` — Vite dev server с hot reload (dev) или nginx (prod build)

---

## План работ (порядок шагов)

0. **Скопировать этот план**: положить файл в `plans/purring-jumping-hellman.md` внутри репо
1. **Подготовка репозитория**: `.gitignore`, root-level README, базовый `docker-compose.yml` с Postgres
2. **Domain**: удалить `Class1.cs`, создать сущности/энумы/value objects
3. **DomainServices**: порты, сервисы (`ReadingService`, `CardDeckService`, `InterpretationService`, `AuthService`), DTO
4. **Infrastructure**: EF `AppDbContext`, репозитории, миграции + сид 78 карт, `OpenAIInterpreter`, JWT/пароли, `AddInfrastructure` extension
5. **Host**: очистить Program.cs, зарегистрировать DI/JWT/CORS/Swagger, написать endpoints, middleware, secrets binding
6. **Unit-тесты** для Domain и DomainServices
7. **Integration-тесты**: Testcontainers fixture, WebApplicationFactory, сценарии readings/auth/history
8. **Frontend**: `npm create vite`, роутер/Pinia/axios, компоненты/views/stores, интеграция с API, анимация переворота карт
9. **Docker**: `backend/Dockerfile`, `frontend/Dockerfile`, доработать `docker-compose.yml`
10. **CI**: оба workflow на сборку/тесты, staging/production деплой
11. **Verification**: прогон всего через docker-compose + ручные проверки

---

## Критические файлы (будущие)

Backend:
- `backend/src/FutureViewer.Host/Program.cs` — DI, middleware, endpoints mapping
- `backend/src/FutureViewer.Host/Endpoints/{Reading,Auth,Card}Endpoints.cs`
- `backend/src/FutureViewer.Infrastructure/Persistence/AppDbContext.cs`
- `backend/src/FutureViewer.Infrastructure/AI/OpenAIInterpreter.cs`
- `backend/src/FutureViewer.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs`
- `backend/src/FutureViewer.DomainServices/Services/ReadingService.cs`
- `backend/src/FutureViewer.DomainServices/Services/InterpretationService.cs`
- `backend/src/FutureViewer.DomainServices/Services/AuthService.cs`
- `backend/tests/FutureViewer.Integration.Tests/Fixtures/IntegrationTestFixture.cs`

Frontend:
- `frontend/src/api/httpClient.ts`
- `frontend/src/stores/useReadingStore.ts`
- `frontend/src/views/ReadingView.vue`, `ResultView.vue`
- `frontend/vite.config.ts` + `.env.*`

Инфра:
- `docker-compose.yml`
- `.github/workflows/backend.yml`, `frontend.yml`
- `.gitignore`

---

## Verification

1. `docker compose up --build` → поднимаются Postgres, backend, frontend
2. `curl -X POST http://localhost:5000/api/readings -d '{"spreadType":"ThreeCard","question":"..."}'` → 3 карты + интерпретация
3. `POST /api/auth/register` → 201, `POST /api/auth/login` → JWT
4. `GET /api/readings/history` с JWT → список раскладов пользователя
5. Открыть `http://localhost:5173` → выбрать расклад → получить AI-интерпретацию в UI
   - Проверить: ambient фон (звёзды, туман), колода «дышит», шафл крутится по эллипсу, карты летят по кривым Безье со stagger'ом, flip воспроизводится с золотой вспышкой, текст интерпретации появляется typewriter'ом
   - DevTools Performance: анимации должны идти в 60fps (композитные слои: transform/opacity)
6. `dotnet test backend/FutureViewer.slnx` → все unit + integration тесты зелёные (Testcontainers сам поднимет Postgres)
7. GitHub Actions: `backend.yml` и `frontend.yml` зелёные на PR
