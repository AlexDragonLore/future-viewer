# Plan: Glossary + Monetization + Navigation + Deck Types

## Context
Приложение Future Viewer — таро-приложение с AI-интерпретациями. Нужно добавить:
1. **Глоссарий** всех 78 карт (хранение в БД, заполнение через миграцию/seed)
2. **Монетизация** — 1 бесплатный расклад на 1 карту в день, подписка 300₽/мес через ЮKassa
3. **Навигация** — верхнее меню с выбором колоды, авторизацией, подпиской, историей
4. **Виды колод** — 5 типов (RWS, Thoth, Marseille, Visconti-Sforza, Modern Witch), влияют на тон интерпретации
5. **Новые расклады** — пользователь пришлёт список позже, архитектура должна поддерживать лёгкое добавление

## Overview
Пять фаз: глоссарий, модель подписки, интеграция ЮKassa, навигация/UI, выбор колоды. Каждая фаза разбита на задачи по слоям Clean Architecture (Domain → DomainServices → Infrastructure → Host → Frontend).

## Success criteria
- [ ] `dotnet test backend/FutureViewer.slnx` — все существующие тесты проходят
- [ ] `npm run build` (frontend) — сборка без ошибок
- [ ] Глоссарий доступен публично на `/glossary`
- [ ] Авторизованный пользователь без подписки видит лимит 1 SingleCard/день
- [ ] Подписка через ЮKassa sandbox активируется корректно
- [ ] Выбор колоды меняет тон AI-интерпретации

---

### Task 1: Domain — новые энумы и расширение сущностей (Phase 1.1)
- [x] Создать `backend/src/FutureViewer.Domain/Enums/DeckType.cs` с 5 значениями (RWS, Thoth, Marseille, ViscontiSforza, ModernWitch)
- [x] Создать `backend/src/FutureViewer.Domain/Enums/SuggestedTone.cs` с 4 значениями (Neutral, Supportive, Strict, Contemplative)
- [x] Расширить `TarotCard.cs` полями NameEn, UprightKeywords, ReversedKeywords, ShortUpright, ShortReversed, SuggestedTone, Aliases, DeckVariants
- [x] Создать `backend/src/FutureViewer.Domain/Entities/DeckVariant.cs` (Id, CardId, DeckType, VariantNote)
- [x] Сборка `dotnet build backend/FutureViewer.slnx` проходит

### Task 2: Infrastructure — EF конфигурация и seed (Phase 1.2)
- [x] Расширить `TarotCardConfiguration.cs` — маппинг новых колонок, jsonb для keywords/aliases
- [x] Создать `DeckVariantConfiguration.cs` — таблица `deck_variants`, уникальный индекс `(card_id, deck_type)`
- [x] Расширить `AppDbContext.cs` — `DbSet<DeckVariant>`
- [x] Переписать `TarotDeckSeed.cs` со всеми 78 картами (новые поля) + метод `BuildDeckVariants()` (390 записей)
- [x] Расширить `DatabaseInitializer.cs` — сидинг DeckVariants, апдейт карт новыми полями
- [x] Создать миграцию `AddGlossaryFields`
- [x] `dotnet build` проходит

### Task 3: DomainServices — DTO и глоссарный сервис (Phase 1.3)
- [x] Создать `DTOs/CardGlossaryDto.cs` и `DTOs/DeckVariantDto.cs`
- [x] Расширить `ICardDeck` методами `GetAllWithVariantsAsync()`, `GetByIdWithVariantsAsync(int id)`
- [x] Расширить `CardDeckRepository` реализациями с `.Include(c => c.DeckVariants)`
- [x] Расширить `CardDeckService` методами `GetGlossaryAsync()`, `GetCardDetailAsync(int id)`
- [x] Добавить unit-тесты для новых методов сервиса

### Task 4: Host — глоссарные API эндпоинты (Phase 1.4)
- [x] Расширить `CardEndpoints.cs` — `GET /api/cards/glossary`, `GET /api/cards/{id:int}`
- [x] Добавить integration-тест для эндпоинтов

### Task 5: Frontend — глоссарий UI (Phase 1.5)
- [x] Создать `frontend/src/api/glossaryApi.ts`
- [x] Создать `frontend/src/stores/useGlossaryStore.ts`
- [x] Создать `GlossaryView.vue` с сеткой 78 карт и фильтром по мастям
- [x] Создать `CardDetailView.vue` с деталями и табами для вариантов колод
- [x] Расширить `router/index.ts` — `/glossary`, `/glossary/:id`
- [x] Расширить `types/index.ts` — `DeckType`, `SuggestedTone`, `CardGlossary`, `DeckVariantInfo`
- [x] `npm run build` проходит

### Task 6: Domain + Infrastructure для подписки (Phase 2.1-2.2)
- [x] Создать `Enums/SubscriptionStatus.cs` (None, Active, Expired, Cancelled)
- [x] Расширить `User.cs` полями SubscriptionStatus, SubscriptionExpiresAt, YukassaSubscriptionId
- [x] Расширить `UserConfiguration.cs`
- [x] Миграция `AddSubscriptionFields`
- [x] Расширить `IReadingRepository` + `ReadingRepository` — `CountTodayByUserAsync(Guid)`
- [x] Расширить `IUserRepository` + `UserRepository` — `UpdateAsync(User)`
- [x] `dotnet build` проходит

### Task 7: SubscriptionService + интеграция с ReadingService (Phase 2.3)
- [x] Создать `Services/SubscriptionService.cs` с `IsReadingAllowedAsync` и `GetStatusAsync`
- [x] Создать `DTOs/SubscriptionStatusDto.cs`
- [x] Создать исключения `QuotaExceededException`, `SubscriptionRequiredException`
- [x] Расширить `ReadingService.CreateAsync`/`CreateStreamAsync` — проверка через `SubscriptionService`
- [x] Расширить `ExceptionHandlerMiddleware` — маппинг 429/402
- [x] Unit-тесты для `SubscriptionService`

### Task 8: Host endpoints подписки + frontend (Phase 2.4-2.5)
- [x] Создать `SubscriptionEndpoints.cs` — `GET /api/subscription/status` с `.RequireAuthorization()`
- [x] Добавить `.RequireAuthorization()` на POST endpoints readings
- [x] Создать `frontend/src/api/subscriptionApi.ts`
- [x] Расширить `useAuthStore.ts` — состояние подписки
- [x] Расширить `HomeView.vue` — бейдж подписки, блокировка

### Task 9: Интеграция ЮKassa (Phase 3)
- [x] Создать `Payment/YukassaOptions.cs` и `Payment/YukassaClient.cs`
- [x] Создать `Interfaces/IPaymentProvider.cs`
- [x] Расширить `SubscriptionService` методами `CreatePaymentAsync`, `ProcessWebhookAsync`
- [x] Создать `PaymentEndpoints.cs` — `POST /api/payments/subscribe`, `POST /api/payments/webhook`
- [x] Создать `frontend/src/api/paymentApi.ts`, `SubscriptionBanner.vue`, `PaymentSuccessView.vue`
- [x] Расширить router — `/payment/success`

### Task 10: Верхнее меню сайта (Phase 4.1)
- [x] Создать `components/SiteHeader.vue` с логотипом, ссылкой на глоссарий, селектором колоды, auth-состоянием
- [x] Расширить `App.vue` — рендер SiteHeader над RouterView, padding-top
- [x] Стилизация: полупрозрачный фон, blur, золотые акценты

### Task 11: Детали расклада из истории (Phase 4.2)
- [x] Создать `views/ReadingDetailView.vue` — маршрут `/reading/:id`
- [x] Расширить `HistoryView.vue` — кликабельные элементы → `/reading/:id`
- [x] Расширить router

### Task 12: Выбор колоды (Phase 5)
- [ ] Расширить `Reading` entity — `DeckType` (default RWS)
- [ ] Расширить `ReadingConfiguration` — колонка `deck_type`
- [ ] Миграция `AddDeckTypeToReading`
- [ ] Расширить `CreateReadingRequest`, `InterpretationService`, `ReadingService`, `OpenAIInterpreter` — учёт `DeckType` в промпте, вариант notes
- [ ] Расширить `ICardDeck` методом `GetVariantNotesAsync`
- [ ] Создать `frontend/src/stores/useDeckStore.ts` (persisted в localStorage `fv_deck`)
- [ ] Активировать dropdown выбора колоды в SiteHeader
- [ ] Расширить `useReadingStore.ts`, `readingApi.ts` — передача `deckType`

---

## Зависимости

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
3. **Расклады остаются в коде** (Spread.cs) — не в БД.
4. **ЮKassa через HTTP API** (нет официального .NET SDK) — basic auth, v3 API.
5. **DeckType по умолчанию = RWS** — обратная совместимость.
