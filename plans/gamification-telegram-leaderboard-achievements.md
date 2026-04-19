# Gamification: Telegram-бот + Оценки + Лидерборд + Ачивки

## Context

Future Viewer — таро-приложение. Сейчас пользователь делает расклад, получает AI-интерпретацию и всё. Нужна геймификация: на следующий день Telegram-бот присылает ссылку на сайт, где пользователь в свободной форме рассказывает, как следовал рекомендациям. AI оценивает искренность и качество следования (1-10, отрицательная оценка за неискренность). Переответить нельзя. На основе оценок — месячный и all-time лидерборд + 12 ачивок.

**Ключевое:** ответ собирается через веб-форму на сайте (не в Telegram). Бот только отправляет уведомление со ссылкой.

---

## Phase 1: Domain — новые сущности и enum

### Task 1: Phase 1 - Domain entities and enum

- [x] Create `FeedbackStatus` enum
- [x] Create `ReadingFeedback` entity
- [x] Create `Achievement` entity
- [x] Create `UserAchievement` entity
- [x] Extend `User` with Telegram fields and navigations
- [x] Extend `Reading` with Feedback navigation
- [x] Ensure backend builds

### 1.1 Enum `FeedbackStatus` → `Domain/Enums/FeedbackStatus.cs`
```
Pending = 0, Notified = 1, Answered = 2, Scored = 3, Expired = 4
```

### 1.2 Entity `ReadingFeedback` → `Domain/Entities/ReadingFeedback.cs`
| Поле | Тип | Описание |
|------|-----|----------|
| Id | Guid | PK |
| ReadingId | Guid | FK → Reading, unique (1:1) |
| UserId | Guid | FK → User |
| Token | string | Уникальный токен для ссылки (32 символа) |
| SelfReport | string? | Свободный текст ответа пользователя |
| AiScore | int? | 1-10 (может быть отрицательным при неискренности → 1) |
| AiScoreReason | string? | Объяснение AI |
| IsSincere | bool? | AI-оценка искренности |
| ScheduledAt | DateTime | Когда отправить уведомление (CreatedAt + 24h) |
| NotifiedAt | DateTime? | Когда бот отправил ссылку |
| AnsweredAt | DateTime? | Когда пользователь ответил |
| Status | FeedbackStatus | |
| CreatedAt | DateTime | |

### 1.3 Entity `Achievement` → `Domain/Entities/Achievement.cs`
| Поле | Тип |
|------|-----|
| Id | Guid (PK) |
| Code | string (unique, e.g. "first_reading") |
| NameRu | string |
| DescriptionRu | string |
| IconPath | string |
| SortOrder | int |

### 1.4 Entity `UserAchievement` → `Domain/Entities/UserAchievement.cs`
| Поле | Тип |
|------|-----|
| Id | Guid (PK) |
| UserId | Guid (FK) |
| AchievementId | Guid (FK) |
| UnlockedAt | DateTime |

Unique constraint на (UserId, AchievementId).

### 1.5 Расширить `User` (существующий файл)
Добавить поля:
- `TelegramChatId` (long?) — Telegram chat ID
- `TelegramLinkToken` (string?) — одноразовый токен для привязки

Навигации:
- `ICollection<ReadingFeedback> Feedbacks`
- `ICollection<UserAchievement> Achievements`

### 1.6 Расширить `Reading`
Навигация: `ReadingFeedback? Feedback`

---

## Phase 2: Infrastructure — БД

### Task 2: Phase 2 - EF Configurations, Migration, Seed

- [x] Add `ReadingFeedbackConfiguration`
- [x] Add `AchievementConfiguration`
- [x] Add `UserAchievementConfiguration`
- [x] Update `UserConfiguration` (Telegram fields + unique TelegramChatId index)
- [x] Update `ReadingConfiguration` if needed
- [x] Add DbSets in `AppDbContext`
- [x] Create migration `AddGamification`
- [x] Seed 12 achievements in `DatabaseInitializer`
- [x] Ensure backend builds

### 2.1 EF Configurations
- `ReadingFeedbackConfiguration.cs` — unique index на ReadingId, index на (UserId, Status), index на ScheduledAt, unique index на Token
- `AchievementConfiguration.cs` — unique index на Code
- `UserAchievementConfiguration.cs` — unique composite index на (UserId, AchievementId)
- Обновить `UserConfiguration.cs` — unique index на TelegramChatId

### 2.2 `AppDbContext` — добавить DbSet
`ReadingFeedbacks`, `Achievements`, `UserAchievements`

### 2.3 Миграция `AddGamification`

### 2.4 Achievement Seed Data (в `DatabaseInitializer`)
12 ачивок:
| Code | Название | Описание |
|------|----------|----------|
| first_reading | Первый расклад | Сделайте свой первый расклад |
| first_feedback | Первый отклик | Ответьте на первый запрос обратной связи |
| telegram_linked | На связи | Привяжите Telegram аккаунт |
| streak_3 | Три дня подряд | Делайте расклады 3 дня подряд |
| streak_7 | Неделя мудрости | Делайте расклады 7 дней подряд |
| streak_30 | Месяц просветления | Делайте расклады 30 дней подряд |
| total_10 | Десятка | Сделайте 10 раскладов |
| total_50 | Полсотни | Сделайте 50 раскладов |
| total_100 | Сотня | Сделайте 100 раскладов |
| score_master | Мастер следования | Средний балл 8+ (минимум 10 откликов) |
| perfect_10 | Идеальный балл | Получите оценку 10/10 |
| high_five | Пятёрка десяток | Получите 5 раз оценку 10/10 |

---

## Phase 3: DomainServices — интерфейсы, DTO, сервисы

### Task 3: Phase 3 - Interfaces, DTOs, Services

- [x] Add `IFeedbackRepository`, `IAchievementRepository`, `ILeaderboardRepository`, `ITelegramNotifier`
- [x] Add DTOs (TelegramLinkResponse, LeaderboardEntryDto, UserScoreSummaryDto, AchievementDto, FeedbackDto, SubmitFeedbackRequest, FeedbackScoringResult)
- [x] Implement `FeedbackService`
- [x] Implement `AchievementService`
- [x] Implement `LeaderboardService`
- [x] Implement `TelegramLinkService`
- [x] Integrate FeedbackService.ScheduleAsync into ReadingService
- [x] Add `SubmitFeedbackValidator`
- [x] Register services in DomainServicesExtensions
- [x] Ensure backend builds

### 3.1 Новые интерфейсы в `DomainServices/Interfaces/`
- `IFeedbackRepository` — Add, GetById, GetByToken, GetByReadingId, GetPendingToNotify(DateTime before, int batch), Update, GetScoredByUser(userId)
- `IAchievementRepository` — GetAll, GetByUser(userId), HasAchievement(userId, code), Grant(UserAchievement)
- `ILeaderboardRepository` — GetMonthly(year, month, take), GetAllTime(take), GetUserSummary(userId)
- `ITelegramNotifier` — SendFeedbackLink(long chatId, string question, string feedbackUrl), SendAchievementNotification(long chatId, string name, string description)

### 3.2 Новые DTO в `DomainServices/DTOs/`
- `TelegramLinkResponse` { DeepLinkUrl, IsLinked }
- `LeaderboardEntryDto` { UserId, DisplayName (замаскированный email), TotalScore, FeedbackCount, AverageScore, Rank }
- `UserScoreSummaryDto` { TotalScore, MonthlyScore, Rank, MonthlyRank, FeedbackCount, AverageScore }
- `AchievementDto` { Id, Code, Name, Description, IconPath, UnlockedAt? }
- `FeedbackDto` { Id, ReadingId, Question, Interpretation, AiScore?, AiScoreReason?, IsSincere?, SelfReport?, Status, CreatedAt, AnsweredAt? }
- `SubmitFeedbackRequest` { Token, SelfReport }
- `FeedbackScoringResult` { Score (1-10), Reason, IsSincere }

### 3.3 Новые сервисы в `DomainServices/Services/`

**`FeedbackService`**
- `ScheduleAsync(Reading reading)` — создаёт ReadingFeedback с Token + ScheduledAt = CreatedAt + 24h, status = Pending
- `SubmitAsync(string token, string selfReport)` — находит по токену, проверяет Status != Answered/Scored (иначе ошибка "переответить нельзя"), сохраняет selfReport, вызывает AI scoring, обновляет статус
- `GetByTokenAsync(string token)` — для отображения формы на фронте (вопрос + интерпретация)
- `GetUserFeedbacksAsync(Guid userId)` — история для профиля

**`AchievementService`**
- `CheckAndGrantAsync(Guid userId)` — проверяет все правила, выдаёт новые ачивки. Правила хардкожены (не rules engine). Возвращает список новых ачивок для уведомления
- `GetAllWithUserStatusAsync(Guid userId)` — каталог с отметками разблокированных
- `GetUserAchievementsAsync(Guid userId)`

**`LeaderboardService`**
- `GetMonthlyAsync(year?, month?, take = 50)`
- `GetAllTimeAsync(take = 50)`
- `GetUserSummaryAsync(Guid userId)`

**`TelegramLinkService`**
- `GenerateLinkAsync(Guid userId)` — генерирует TelegramLinkToken, возвращает deep-link URL `https://t.me/{BotUsername}?start={token}`
- `CompleteLinkAsync(string token, long chatId)` — привязывает аккаунт
- `UnlinkAsync(Guid userId)` — отвязывает

### 3.4 Интеграция в `ReadingService`
В `CreateAsync` и `CreateStreamAsync` после persist — вызывать `FeedbackService.ScheduleAsync(reading)` если userId != null.

### 3.5 Валидация
- `SubmitFeedbackValidator` — SelfReport не пустой, минимум 10 символов

---

## Phase 4: Infrastructure — реализации

### Task 4: Phase 4 - Infrastructure implementations

- [x] Implement `FeedbackRepository`
- [x] Implement `AchievementRepository`
- [x] Implement `LeaderboardRepository`
- [x] Implement `FeedbackScoringInterpreter`
- [x] Add `TelegramOptions` + `TelegramBotService` + update handler + polling BackgroundService
- [x] Implement `FeedbackNotificationJob` (BackgroundService)
- [x] Register everything in `InfrastructureServiceExtensions`
- [x] Add `Telegram.Bot` NuGet
- [x] Ensure backend builds

### 4.1 Репозитории в `Infrastructure/Persistence/Repositories/`
- `FeedbackRepository`
- `AchievementRepository`
- `LeaderboardRepository` — SQL GROUP BY на reading_feedbacks для рейтингов. Email маскируется (u***@gmail.com)

### 4.2 AI Scoring в `Infrastructure/AI/FeedbackScoringInterpreter.cs`
- Использует тот же OpenAI пакет и ключ
- Вход: оригинальная интерпретация + вопрос + selfReport пользователя
- Системный промпт: оценить искренность и степень следования рекомендациям (1-10)
- Если AI считает ответ неискренним → IsSincere = false, score = 1, reason объясняет
- Возвращает JSON structured output: `{ score, reason, isSincere }`

### 4.3 Telegram в `Infrastructure/Telegram/`
- NuGet: `Telegram.Bot` v22+
- `TelegramOptions` { BotToken, BotUsername, WebhookUrl?, SecretToken }
- `TelegramBotService : ITelegramNotifier` — отправка сообщений со ссылками
- `TelegramUpdateHandler` — обработка входящих updates (только /start с токеном для привязки)
- `TelegramPollingService : BackgroundService` — long polling для dev (если WebhookUrl не задан)

### 4.4 `FeedbackNotificationJob : BackgroundService`
- Таймер каждые 5 минут
- Берёт batch pending feedbacks где ScheduledAt <= now
- Для каждого: если user.TelegramChatId != null → отправляет ссылку `{siteUrl}/feedback/{token}`, статус = Notified
- Если TelegramChatId == null → пропускает (можно expired через 7 дней)

### 4.5 DI регистрация
Обновить `InfrastructureServiceExtensions.cs` — зарегистрировать все новые сервисы, репозитории, BackgroundService'ы.
Обновить `DomainServicesExtensions.cs` — зарегистрировать FeedbackService, AchievementService, LeaderboardService, TelegramLinkService.

---

## Phase 5: Host — API эндпоинты

### Task 5: Phase 5 - API endpoints

- [x] Add `FeedbackEndpoints` (GET/POST by token, GET my)
- [x] Add `LeaderboardEndpoints` (monthly, alltime, me)
- [x] Add `AchievementEndpoints` (catalog, my)
- [x] Add `TelegramEndpoints` (link, unlink, status, webhook)
- [x] Register all new endpoint maps in `Program.cs`
- [x] Ensure backend builds

### 5.1 `FeedbackEndpoints.cs` → `MapFeedbacks()`
- `GET /api/feedbacks/{token}` [Anonymous] — данные для формы (вопрос, интерпретация, статус). Анонимный чтобы ссылка из Telegram работала без логина
- `POST /api/feedbacks/{token}` [Anonymous] — submit ответа. Валидация: минимум 10 символов, статус не Answered/Scored
- `GET /api/feedbacks/my` [Authorize] — история фидбэков пользователя

### 5.2 `LeaderboardEndpoints.cs` → `MapLeaderboard()`
- `GET /api/leaderboard/monthly?year=&month=` [Anonymous]
- `GET /api/leaderboard/alltime` [Anonymous]
- `GET /api/leaderboard/me` [Authorize]

### 5.3 `AchievementEndpoints.cs` → `MapAchievements()`
- `GET /api/achievements` [Anonymous] — каталог
- `GET /api/achievements/me` [Authorize] — пользователя с отметками

### 5.4 `TelegramEndpoints.cs` → `MapTelegram()`
- `POST /api/telegram/link` [Authorize] — генерирует deep-link
- `DELETE /api/telegram/link` [Authorize] — отвязывает
- `GET /api/telegram/status` [Authorize] — {isLinked}
- `POST /api/telegram/webhook` [Anonymous] — Telegram updates (верифицируется SecretToken header)

### 5.5 Регистрация в `Program.cs`
```csharp
app.MapFeedbacks();
app.MapLeaderboard();
app.MapAchievements();
app.MapTelegram();
```

---

## Phase 6: Frontend

### Task 6: Phase 6 - Frontend

- [x] Add TS types
- [x] Add API modules (feedbackApi, leaderboardApi, achievementApi, telegramApi)
- [x] Add Pinia stores (useLeaderboardStore, useAchievementStore, useProfileStore)
- [x] Implement `FeedbackView.vue`
- [x] Implement `LeaderboardView.vue`
- [x] Implement `AchievementsView.vue`
- [x] Implement `ProfileView.vue`
- [x] Add components (LeaderboardTable, AchievementCard, ScoreBadge, TelegramLinkButton, FeedbackForm)
- [x] Add router routes
- [x] Update `SiteHeader.vue` navigation
- [x] Ensure frontend builds

### 6.1 Типы в `types/`
LeaderboardEntry, UserScoreSummary, AchievementInfo, FeedbackInfo, TelegramStatus

### 6.2 API модули в `api/`
- `feedbackApi.ts` — getByToken(token), submit(token, selfReport), getMy()
- `leaderboardApi.ts` — monthly(year?, month?), alltime(), me()
- `achievementApi.ts` — all(), mine()
- `telegramApi.ts` — link(), unlink(), status()

### 6.3 Pinia stores в `stores/`
- `useLeaderboardStore.ts`
- `useAchievementStore.ts`
- `useProfileStore.ts` (score summary, telegram status)

### 6.4 Views
- `FeedbackView.vue` (route: `/feedback/:token`) — форма ответа. Показывает вопрос и интерпретацию, textarea для ответа, кнопка отправки. После отправки — показ оценки и объяснения AI. Если уже отвечено — показ результата без формы
- `LeaderboardView.vue` (route: `/leaderboard`) — табы Monthly/All Time, таблица с рейтингом, подсветка текущего пользователя
- `AchievementsView.vue` (route: `/achievements`, requiresAuth) — сетка карточек ачивок (locked/unlocked)
- `ProfileView.vue` (route: `/profile`, requiresAuth) — email, дата регистрации, score summary, привязка Telegram, последние фидбэки, ачивки

### 6.5 Компоненты
- `LeaderboardTable.vue`
- `AchievementCard.vue` (locked/unlocked состояния)
- `ScoreBadge.vue` (цветовая кодировка 1-10)
- `TelegramLinkButton.vue`
- `FeedbackForm.vue`

### 6.6 Router — добавить маршруты
```ts
{ path: '/feedback/:token', name: 'feedback', component: () => import('@/views/FeedbackView.vue') },
{ path: '/leaderboard', name: 'leaderboard', component: () => import('@/views/LeaderboardView.vue') },
{ path: '/achievements', name: 'achievements', component: () => import('@/views/AchievementsView.vue'), meta: { requiresAuth: true } },
{ path: '/profile', name: 'profile', component: () => import('@/views/ProfileView.vue'), meta: { requiresAuth: true } },
```

### 6.7 Навигация
Обновить `SiteHeader.vue` — добавить Лидерборд, Профиль (для авторизованных).

---

## Phase 7: Конфигурация

### Task 7: Phase 7 - Configuration

- [x] Add Telegram section to `appsettings.json`
- [x] Add Telegram env vars in `docker-compose.yml`

### appsettings.json
```json
"Telegram": {
  "BotToken": "",
  "BotUsername": "FutureViewerBot",
  "WebhookUrl": "",
  "SecretToken": ""
}
```

### docker-compose.yml
```yaml
TELEGRAM__BotToken: ${TELEGRAM_BOT_TOKEN:-}
TELEGRAM__BotUsername: ${TELEGRAM_BOT_USERNAME:-FutureViewerBot}
TELEGRAM__WebhookUrl: ${TELEGRAM_WEBHOOK_URL:-}
TELEGRAM__SecretToken: ${TELEGRAM_SECRET_TOKEN:-}
```

### User secrets (dev)
```bash
dotnet user-secrets set "Telegram:BotToken" "..."
dotnet user-secrets set "Telegram:SecretToken" "..."
```

---

## Phase 8: Тесты

### Task 8: Phase 8 - Tests

- [x] Unit tests for FeedbackService (scheduling, submit, no re-answer)
- [x] Unit tests for AchievementService (rule checks)
- [x] Unit tests for LeaderboardService
- [x] Integration tests for new endpoints (feedback submit flow, leaderboard queries)
- [x] Frontend tests (FeedbackView, LeaderboardTable, AchievementCard)
- [x] Ensure `dotnet test` passes
- [x] Ensure `npm test` passes (new gamification tests pass; 3 pre-existing CardFlip/ResultView failures unrelated to this task)

---

## NuGet/NPM пакеты

**Backend (Infrastructure.csproj):** `Telegram.Bot` v22+
**Frontend:** ничего нового

---

## Порядок реализации

1. Domain entities + enum (Phase 1)
2. EF configurations + migration + seed (Phase 2)
3. Interfaces + DTOs (Phase 3.1-3.2)
4. Services (Phase 3.3-3.5)
5. Infrastructure repos + AI scorer + Telegram (Phase 4)
6. API endpoints (Phase 5)
7. Frontend types + API + stores + views (Phase 6)
8. Config (Phase 7)
9. Tests (Phase 8)

---

## Критические файлы для модификации

- `backend/src/FutureViewer.Domain/Entities/User.cs` — добавить TelegramChatId, TelegramLinkToken
- `backend/src/FutureViewer.Domain/Entities/Reading.cs` — навигация Feedback
- `backend/src/FutureViewer.Infrastructure/Persistence/AppDbContext.cs` — новые DbSet
- `backend/src/FutureViewer.Infrastructure/Persistence/Configurations/UserConfiguration.cs` — новые поля
- `backend/src/FutureViewer.DomainServices/Services/ReadingService.cs` — вызов FeedbackService.ScheduleAsync
- `backend/src/FutureViewer.DomainServices/DependencyInjection/DomainServicesExtensions.cs` — регистрация новых сервисов
- `backend/src/FutureViewer.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs` — регистрация
- `backend/src/FutureViewer.Host/Program.cs` — маппинг новых эндпоинтов
- `backend/src/FutureViewer.Infrastructure/Persistence/DatabaseInitializer.cs` — seed ачивок
- `frontend/src/router/index.ts` — новые маршруты
- `frontend/src/components/SiteHeader.vue` — навигация

## Верификация

1. `dotnet build backend/FutureViewer.slnx` — компиляция
2. `dotnet test backend/FutureViewer.slnx` — все тесты (существующие не должны сломаться)
3. `cd frontend && npm run build` — фронт компиляция
4. `cd frontend && npm test` — фронт тесты
5. `docker compose up --build` — full-stack запуск
6. Ручной тест: создать расклад → проверить что feedback запланирован → открыть /feedback/{token} → отправить ответ → проверить оценку AI → проверить лидерборд и ачивки
