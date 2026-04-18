# Plan: Glossary + Monetization + Navigation + Deck Types

> **Step 0**: Скопировать этот план в `plans/glossary-monetization-navigation.md` в репозитории сразу после ExitPlanMode.

## Context
Приложение Future Viewer — таро-приложение с AI-интерпретациями. Нужно добавить:
1. **Глоссарий** всех 78 карт (хранение в БД, заполнение через миграцию/seed)
2. **Монетизация** — 1 бесплатный расклад на 1 карту в день, подписка 300₽/мес через ЮKassa
3. **Навигация** — верхнее меню с выбором колоды, авторизацией, подпиской, историей
4. **Виды колод** — 5 типов (RWS, Thoth, Marseille, Visconti-Sforza, Modern Witch), влияют на тон интерпретации
5. **Новые расклады** — пользователь пришлёт список позже, архитектура должна поддерживать лёгкое добавление

Ресёрч-документ `/Users/aleksandr/RiderProjects/deep-research-report.md.md` содержит полную JSON-схему глоссария с полями, примерами для 8 карт, описаниями 5 колод и правилами генерации.

---

## Phase 1: Глоссарий + расширение БД

### 1.1 Domain — новые энумы и расширение сущностей

**Создать** `backend/src/FutureViewer.Domain/Enums/DeckType.cs`:
```csharp
public enum DeckType { RWS = 1, Thoth = 2, Marseille = 3, ViscontiSforza = 4, ModernWitch = 5 }
```

**Создать** `backend/src/FutureViewer.Domain/Enums/SuggestedTone.cs`:
```csharp
public enum SuggestedTone { Neutral = 0, Supportive = 1, Strict = 2, Contemplative = 3 }
```

**Расширить** `backend/src/FutureViewer.Domain/Entities/TarotCard.cs` — добавить:
- `NameEn` (string, required, max 128)
- `UprightKeywords` (List\<string\>, хранить как jsonb)
- `ReversedKeywords` (List\<string\>, jsonb)
- `ShortUpright` (string, max 300) — краткое значение для глоссария
- `ShortReversed` (string, max 300)
- `SuggestedTone` (SuggestedTone enum)
- `Aliases` (List\<string\>?, jsonb)
- `ICollection<DeckVariant> DeckVariants` — навигационное свойство

**Создать** `backend/src/FutureViewer.Domain/Entities/DeckVariant.cs`:
- `Id` (int, required), `CardId` (int, FK), `DeckType` (DeckType enum), `VariantNote` (string, max 500)

### 1.2 Infrastructure — EF конфигурация и seed

**Расширить** `backend/src/FutureViewer.Infrastructure/Persistence/Configurations/TarotCardConfiguration.cs`:
- Маппинг новых колонок, jsonb для keywords/aliases

**Создать** `backend/src/FutureViewer.Infrastructure/Persistence/Configurations/DeckVariantConfiguration.cs`:
- Таблица `deck_variants`, уникальный индекс `(card_id, deck_type)`

**Расширить** `backend/src/FutureViewer.Infrastructure/Persistence/AppDbContext.cs`:
- Добавить `DbSet<DeckVariant>`

**Переписать** `backend/src/FutureViewer.Infrastructure/Persistence/TarotDeckSeed.cs`:
- Все 78 карт с новыми полями (NameEn, keywords, short texts, tone, aliases)
- Новый метод `BuildDeckVariants()` — 390 записей (78 × 5 колод)
- Данные взять из ресёрч-документа, дополнив оставшиеся 70 карт по аналогии

**Расширить** `backend/src/FutureViewer.Infrastructure/Persistence/DatabaseInitializer.cs`:
- Сидинг DeckVariants после карт
- Метод обновления существующих карт новыми полями

**Создать миграцию**: `dotnet ef migrations add AddGlossaryFields`

### 1.3 DomainServices — DTO и сервис глоссария

**Создать** `backend/src/FutureViewer.DomainServices/DTOs/CardGlossaryDto.cs`:
- Все поля карты + список `DeckVariantDto`

**Расширить** `backend/src/FutureViewer.DomainServices/Interfaces/ICardDeck.cs`:
- `GetAllWithVariantsAsync()`, `GetByIdWithVariantsAsync(int id)`

**Расширить** `backend/src/FutureViewer.Infrastructure/Persistence/Repositories/CardDeckRepository.cs`:
- Реализация новых методов с `.Include(c => c.DeckVariants)`

**Расширить** `backend/src/FutureViewer.DomainServices/Services/CardDeckService.cs`:
- `GetGlossaryAsync()`, `GetCardDetailAsync(int id)`

### 1.4 Host — API эндпоинты

**Расширить** `backend/src/FutureViewer.Host/Endpoints/CardEndpoints.cs`:
- `GET /api/cards/glossary` — все 78 карт с глоссарными данными
- `GET /api/cards/{id:int}` — детали одной карты с вариантами колод

### 1.5 Frontend — страница глоссария

**Создать** `frontend/src/api/glossaryApi.ts`
**Создать** `frontend/src/stores/useGlossaryStore.ts`
**Создать** `frontend/src/views/GlossaryView.vue`:
- Сетка 78 карт, фильтр по мастям (All / Major / Wands / Cups / Swords / Pentacles)
- Клик открывает детальный вид карты

**Создать** `frontend/src/views/CardDetailView.vue`:
- Полная информация: имя RU/EN, ключевые слова, описания, варианты колод табами

**Расширить** `frontend/src/router/index.ts`:
- `/glossary`, `/glossary/:id`

**Расширить** `frontend/src/types/index.ts`:
- `DeckType`, `SuggestedTone`, `CardGlossary`, `DeckVariantInfo`

---

## Phase 2: Модель подписки (backend)

### 2.1 Domain

**Создать** `backend/src/FutureViewer.Domain/Enums/SubscriptionStatus.cs`:
```csharp
public enum SubscriptionStatus { None = 0, Active = 1, Expired = 2, Cancelled = 3 }
```

**Расширить** `backend/src/FutureViewer.Domain/Entities/User.cs`:
- `SubscriptionStatus` (enum, default None)
- `SubscriptionExpiresAt` (DateTime?)
- `YukassaSubscriptionId` (string?, max 128)

### 2.2 Infrastructure

**Расширить** `UserConfiguration.cs` — новые колонки
**Миграция**: `AddSubscriptionFields`

**Расширить** `IReadingRepository` + `ReadingRepository`:
- `CountTodayByUserAsync(Guid userId)` — подсчёт раскладов за сегодня

**Расширить** `IUserRepository` + `UserRepository`:
- `UpdateAsync(User user)`

### 2.3 DomainServices

**Создать** `backend/src/FutureViewer.DomainServices/Services/SubscriptionService.cs`:
- `IsReadingAllowedAsync(Guid userId, SpreadType)` — подписка: всё доступно; без подписки: только SingleCard, макс 1/день
- `GetStatusAsync(Guid userId)` → `SubscriptionStatusDto`

**Создать** DTOs: `SubscriptionStatusDto`

**Создать** исключения: `QuotaExceededException`, `SubscriptionRequiredException`

**Расширить** `ReadingService.CreateAsync` / `CreateStreamAsync`:
- Проверка через `SubscriptionService.IsReadingAllowedAsync()` перед созданием
- Авторизация обязательна для всех раскладов

**Расширить** `ExceptionHandlerMiddleware`:
- `QuotaExceededException` → 429
- `SubscriptionRequiredException` → 402

### 2.4 Host

**Создать** `backend/src/FutureViewer.Host/Endpoints/SubscriptionEndpoints.cs`:
- `GET /api/subscription/status` (auth) → `SubscriptionStatusDto`

**Расширить** `ReadingEndpoints` — `.RequireAuthorization()` на POST endpoints

### 2.5 Frontend

**Создать** `frontend/src/api/subscriptionApi.ts`
**Расширить** `useAuthStore.ts` — состояние подписки, `loadSubscription()`
**Расширить** `HomeView.vue` — бейдж подписки, блокировка раскладов для бесплатных

---

## Phase 3: Интеграция ЮKassa

### 3.1 Infrastructure

**Создать** `backend/src/FutureViewer.Infrastructure/Payment/YukassaOptions.cs`:
- `ShopId`, `SecretKey`, `ReturnUrl`, `WebhookSecret`

**Создать** `backend/src/FutureViewer.Infrastructure/Payment/YukassaClient.cs`:
- HTTP клиент для YuKassa API v3 (basic auth shopId:secretKey)
- `CreatePaymentAsync(amount, currency, description, userId)` → URL оплаты
- `GetPaymentAsync(paymentId)` → статус

**Создать** `backend/src/FutureViewer.DomainServices/Interfaces/IPaymentProvider.cs`

### 3.2 DomainServices

**Расширить** `SubscriptionService`:
- `CreatePaymentAsync(Guid userId)` → confirmation URL
- `ProcessWebhookAsync(payload)` → обновление подписки пользователя

### 3.3 Host

**Создать** `backend/src/FutureViewer.Host/Endpoints/PaymentEndpoints.cs`:
- `POST /api/payments/subscribe` (auth) → `{ confirmationUrl }`
- `POST /api/payments/webhook` (валидация подписи) → обработка callback

### 3.4 Frontend

**Создать** `frontend/src/api/paymentApi.ts`
**Создать** компонент `SubscriptionBanner.vue` — кнопка "Подписка 300₽/мес"
**Создать** `frontend/src/views/PaymentSuccessView.vue` — страница после оплаты
**Расширить** `router/index.ts` — `/payment/success`

---

## Phase 4: Навигационное меню + UI

### 4.1 Верхнее меню

**Создать** `frontend/src/components/SiteHeader.vue`:
- Лого/название → `/`
- Ссылка "Глоссарий" → `/glossary`
- Селектор колоды (dropdown, 5 вариантов)
- Не авторизован: кнопка "Войти" → `/auth`
- Авторизован без подписки: email + "Подписка" + "История" + "Выход"
- Авторизован с подпиской: email + бейдж + "История" + "Выход"
- Стиль: полупрозрачный фон, `backdrop-filter: blur`, золотые акценты

**Расширить** `frontend/src/App.vue`:
- Рендер `SiteHeader` над `<RouterView>`
- Padding-top для контента

### 4.2 Детали расклада из истории

**Создать** `frontend/src/views/ReadingDetailView.vue`:
- Маршрут `/reading/:id`
- Загрузка расклада через `readingApi.get(id)`
- Отображение как ResultView, но для исторического расклада

**Расширить** `HistoryView.vue`:
- Кликабельные элементы списка → `/reading/:id`

**Расширить** `router/index.ts` — `/reading/:id`

---

## Phase 5: Выбор колоды

### 5.1 Backend

**Расширить** `Reading` entity — `DeckType` (default RWS)
**Расширить** `ReadingConfiguration.cs` — колонка `deck_type`
**Миграция**: `AddDeckTypeToReading`

**Расширить** `CreateReadingRequest` — `DeckType`
**Расширить** `InterpretationService` — передача `DeckType` в промпт
**Расширить** `ReadingService` — передача `DeckType` и загрузка variant notes для карт

**Расширить** `OpenAIInterpreter`:
- Системный промпт зависит от колоды (тон, стиль)
- Variant notes для каждой карты добавляются в контекст

**Расширить** `ICardDeck`:
- `GetVariantNotesAsync(DeckType, IEnumerable<int> cardIds)` → словарь notes

### 5.2 Frontend

**Создать** `frontend/src/stores/useDeckStore.ts`:
- `selectedDeck: DeckType` (persisted в localStorage `fv_deck`)

**Расширить** `SiteHeader.vue` — активировать dropdown выбора колоды
**Расширить** `useReadingStore.ts` — передача `deckType` в API
**Расширить** `readingApi.ts` — `deckType` в body POST запросов

---

## Зависимости между фазами

```
Phase 1 (Глоссарий) ← независимая, начинаем с неё
Phase 2 (Подписка)  ← зависит от Phase 1 (DeckType enum)
Phase 3 (ЮKassa)    ← зависит от Phase 2
Phase 4 (Навигация) ← зависит от Phase 1 + Phase 2
Phase 5 (Колоды)    ← зависит от Phase 1 + Phase 4
```

## Ключевые решения

1. **Авторизация обязательна** для создания раскладов (упрощает rate limiting). Глоссарий публичный.
2. **Seed через DatabaseInitializer** (не HasData) — расширить существующий паттерн TarotDeckSeed.
3. **Расклады остаются в коде** (Spread.cs) — не в БД. Добавление нового расклада = новое значение в SpreadType + case в Spread.From().
4. **ЮKassa через HTTP API** (нет официального .NET SDK) — basic auth, v3 API.
5. **DeckType по умолчанию = RWS** — обратная совместимость с существующими данными.

## Верификация

1. `dotnet test backend/FutureViewer.slnx` — все существующие тесты проходят
2. `npm run build` (frontend) — сборка без ошибок
3. `docker compose up --build` — полный стек работает
4. Ручная проверка: открыть глоссарий, создать расклад, проверить лимит бесплатного расклада
5. Для ЮKassa — тестовый магазин в sandbox-режиме
