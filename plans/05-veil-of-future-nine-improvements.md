# Future Viewer → «Вуаль Грядущего» — девять направлений

## Context

Пакет из 9 разноплановых доработок проекта:
1. Добавить собственную лицензию с условием «20% с оборота» для коммерческого использования.
2. Добавить описания **колод** (Thoth, RWS, Marseille, Visconti–Sforza, Modern Witch) и **раскладов** в глоссарий и показывать их на главной.
3. Подключить lucide-иконки для ачивок (сейчас `iconPath` в БД есть, но фронт рисует эмодзи).
4. Учитывать ачивки в подсчёте рейтинга в лидерборде (сумма очков ачивки + фидбек = общий рейтинг).
5. Сделать сайт адаптивным под мобильные (≤375 px).
6. В модалке «О нас» добавить строку о том, что ИИ учится на фидбэке пользователей.
7. Переименовать сайт на русский («Вуаль Грядущего») везде, где светится «Future Viewer».
8. Убрать у Telegram-бота вебхуки, оставить только long polling.
9. Подтверждение email при регистрации + восстановление пароля через email; почта саппорта в конфиге, отображается в футере (саппорт-почта — без подтверждения, она не юзер).

Цель — одна серия PR, но каждый пункт самодостаточен и может мержиться отдельно.

## Iteration checklist

Порядок соответствует рекомендованному в разделе «Порядок работы и критические файлы» ниже.

### Iteration 1: Пункт 8 — Telegram webhook → polling only
- [x] Удалить endpoint `POST /api/telegram/webhook` и связанные usings/константы из `TelegramEndpoints.cs`
- [x] Убрать `WebhookUrl` и `SecretToken` из `TelegramOptions.cs`
- [x] В `TelegramPollingHostedService` удалить ветку «если webhook задан — не стартуем»; вызвать `DeleteWebhook` перед `ReceiveAsync`
- [x] Почистить `appsettings.json` и `docker-compose.yml` от `WebhookUrl`/`SecretToken`
- [x] Удалить webhook-тест в `TelegramEndpointTests.cs`
- [x] Обновить `CLAUDE.md` (убрать упоминания webhook endpoint и `SecretToken`/`WebhookUrl`)
- [x] `dotnet build` и `dotnet test backend/FutureViewer.slnx` — зелёные

### Iteration 2: Пункт 1 — Лицензия «20% от оборота»
- [x] Создать `LICENSE` в корне (RU + EN)
- [x] Добавить секцию «Лицензия» в `README.md`

### Iteration 3: Пункт 6 — «Мы учимся на ваших откликах» в «О нас»
- [x] Вставить абзац в модалку `SiteFooter.vue`
- [x] Проверить мобильную верстку модалки

### Iteration 4: Пункт 9a — Саппорт-почта в конфиге + в футере
- [x] Секция `Support:Email` в `appsettings.json` + env в `docker-compose.yml`
- [x] Endpoint `GET /api/public/config`
- [x] `publicApi.ts` + `usePublicConfigStore.ts`
- [x] Строка с `mailto:` в `SiteFooter.vue`

### Iteration 5: Пункт 3 — lucide-иконки ачивок
- [x] Установить `lucide-vue-next`
- [x] Добавить `frontend/src/data/achievementIcons.ts`
- [x] Обновить `AchievementCard.vue` на динамический `<component :is>`
- [x] Юнит-тест `AchievementCard.spec.ts`

### Iteration 6: Пункт 7 — Русификация на «Вуаль Грядущего»
- [x] Заменить «Future Viewer» в UI-файлах (index.html, SiteHeader, HomeView, SiteFooter)
- [x] Обновить Yukassa description и TelegramUpdateHandler
- [x] Проверить отсутствие английских вкраплений на страницах

### Iteration 7: Пункт 2 — Колоды и расклады в глоссарии и на главной
- [x] Источники: `frontend/src/data/decks.ts`, `frontend/src/data/spreads.ts`
- [x] Секции `#decks` и `#spreads` в `GlossaryView.vue`
- [x] Блоки описаний в `HomeView.vue`
- [x] `SiteHeader.vue` использует `DECKS`

### Iteration 8: Пункт 4 — Ачивки влияют на рейтинг лидерборда
- [x] `Achievement.Points` + EF миграция `AddAchievementPoints`
- [x] `DatabaseInitializer` проставляет очки (idempotent)
- [x] `LeaderboardRepository.GetMonthlyAsync/GetAllTimeAsync` объединяют фидбек + ачивки
- [x] DTO + фронт-типы + `LeaderboardTable.vue`
- [x] Интеграционный + фронт-тест

### Iteration 9: Пункт 9b — Подтверждение email при регистрации
- [x] Поля `IsEmailVerified` / `EmailVerificationToken` / `EmailVerificationSentAt`
- [x] EF миграция `AddEmailVerification`
- [x] `IEmailSender` + `SmtpEmailSender` (MailKit)
- [x] `AuthService.Register/VerifyEmail/ResendVerification/Login` обновления
- [x] Endpoints `verify-email`, `resend-verification`; `register` → 202 без JWT
- [x] Фронт: `RegisterView`, `VerifyEmailView`, `LoginView` (+ роут)
- [x] Тесты

### Iteration 10: Пункт 9c — Восстановление пароля
- [x] Поля `PasswordResetToken` / `PasswordResetTokenExpiresAt` (можно смерджить миграции)
- [x] `AuthService.ForgotPassword/ResetPassword`
- [x] Endpoints `forgot-password`, `reset-password`
- [x] Фронт: `ForgotPasswordView`, `ResetPasswordView`, ссылка в `LoginView` (+ роуты)
- [x] Тесты

### Iteration 11: Пункт 5 — Мобильная адаптация
- [x] `HomeView.vue` grid-cols-1 на мобиле
- [x] `ReadingView` + композаблы — адаптивная ширина/высота
- [x] `ResultView.vue` flex-wrap + clamp gap
- [x] Бургер-меню в `SiteHeader.vue`
- [x] Таблицы: Admin*/Leaderboard — адаптивы
- [x] Ручной проход iPhone SE / Pixel 7 / iPad (skipped — not automatable)

---

## 1. Лицензия «20% от оборота» (репозиторий)

**Файлы:**
- `LICENSE` (новый, корень)
- `README.md` (секция Licensing)

**Подход:**
- Создать custom source-available лицензию на основе шаблона **PolyForm Shield 1.0.0** либо **Commons Clause + MIT**: код открыт для изучения/некоммерческого использования, для коммерческой эксплуатации требуется заключение отдельного соглашения и выплата 20% gross revenue автору (Александр Дунцев, контакт — utre43@gmail.com).
- Текст — на русском + английский перевод (в одном файле, две секции).
- Дисклеймер: лицензию стоит валидировать юристом до использования в реальных спорах.
- В `README.md` добавить секцию «Лицензия» со ссылкой на `LICENSE` и краткой TL;DR.

**Верификация:** `LICENSE` файл присутствует, GitHub корректно отображает его в сайдбаре репозитория.

---

## 2. Колоды и расклады в глоссарии и на главной

**Файлы:**
- `frontend/src/data/decks.ts` (новый) — источник истины по колодам.
- `frontend/src/data/spreads.ts` (новый) — расширенные описания раскладов (сверх того, что возвращает API `GET /api/cards/spreads`).
- `frontend/src/views/GlossaryView.vue` — добавить два новых раздела: `#decks` («Колоды») и `#spreads` («Расклады») с якорными ссылками.
- `frontend/src/views/HomeView.vue` — рядом с выбором расклада показать:
  - блок «Колода: Thoth — [короткое описание]. [Подробнее →]» (привязан к текущему `useDeckStore`);
  - под каждой карточкой расклада — короткое описание (подтягивается из `data/spreads.ts`) и ссылка «Подробнее в глоссарии».
- `frontend/src/stores/useDeckStore.ts` — опционально использовать `DECKS` как single source.
- `frontend/src/components/SiteHeader.vue` — `deckOptions` переехали в `data/decks.ts`.

**Подход:**
1. `frontend/src/data/decks.ts`: `DECKS: { value: DeckType; label: string; shortDescription: string; longDescription: string; anchorId: string }[]` — 5 колод (RWS, Thoth, Marseille, Visconti–Sforza, Modern Witch), описания на русском (история школы, особенности, для каких запросов подходит).
2. `frontend/src/data/spreads.ts`: `SPREADS_META: { type: SpreadType; shortDescription: string; longDescription: string; anchorId: string }[]` — 3 расклада (SingleCard, ThreeCard, CelticCross).
3. `SiteHeader.vue` перекладывает `deckOptions` на `DECKS`.
4. `HomeView.vue`:
   - над пикером расклада — компактный блок «Сейчас: колода {label}. {shortDescription} <router-link to='/glossary#deck-{anchorId}'>Подробнее</router-link>»;
   - в каждом тайле расклада — добавить `<p class="text-xs text-mystic-silver/60">{shortDescription}</p>` и кнопку-ссылку «Подробнее в глоссарии» → `/glossary#spread-{anchorId}`.
5. `GlossaryView.vue` — две новые секции `<section id="decks">` и `<section id="spreads">` с `v-for`, каждая карточка имеет `id="deck-{anchorId}"`/`id="spread-{anchorId}"` для якорного скролла.

**Верификация:** `/glossary#deck-thoth` корректно скроллит к Thoth; на `HomeView` видны описания колоды и раскладов; смена колоды в хэдере обновляет описание на главной; клик по «Подробнее» переводит в нужный раздел глоссария.

---

## 3. Изображения для ачивок (lucide)

**Файлы:**
- `frontend/package.json` — добавить зависимость `lucide-vue-next`.
- `frontend/src/data/achievementIcons.ts` (новый) — мапа `achievementCode → LucideIconComponent`.
- `frontend/src/components/AchievementCard.vue` — заменить хардкод эмодзи (`✦`/`✧`) на динамический `<component :is="iconComponent" />`.

**Подход:**
1. Установить `lucide-vue-next` (treeshake-friendly, уже используется в Vue-экосистеме).
2. `frontend/src/data/achievementIcons.ts`:
   ```ts
   import { Scroll, MessageSquareHeart, Send, Flame, Sparkles, Crown, CircleCheck, Medal, Trophy, Star, Award, Gem } from 'lucide-vue-next'
   export const ACHIEVEMENT_ICONS: Record<string, Component> = {
     first_reading: Scroll,
     first_feedback: MessageSquareHeart,
     telegram_linked: Send,
     streak_3: Flame,
     streak_7: Flame,
     streak_30: Flame,
     total_10: CircleCheck,
     total_50: Medal,
     total_100: Trophy,
     score_master: Crown,
     perfect_10: Star,
     high_five: Award,
   }
   ```
3. `AchievementCard.vue`: убрать `<span class="icon-glyph">...</span>`, вместо него — `<component :is="iconFor(achievement.code)" :size="48" :stroke-width="1.5" />`. Для locked — тот же компонент, но через CSS класс `.locked .icon { color: var(--mystic-silver-dim); opacity: 0.4; }`.
4. `iconPath` в бэке **оставить как есть** (для возможного будущего использования — например, в Telegram-превью). Фронт его просто игнорирует.
5. Добавить простой unit-тест `AchievementCard.spec.ts`: для каждого из 12 кодов рендерится lucide-иконка (не глиф по умолчанию).

**Верификация:** `npm run dev` → `/achievements` → 12 тайлов с разными lucide-иконками; у заблокированных серый тон, у разблокированных — золотой/акцентный.

---

## 4. Ачивки влияют на рейтинг лидерборда

**Файлы:**
- `backend/src/FutureViewer.Domain/Entities/Achievement.cs` — добавить `public int Points { get; init; }`.
- `backend/src/FutureViewer.Infrastructure/Persistence/Configurations/AchievementConfiguration.cs` — колонка `points` (int, not null, default 0).
- `backend/src/FutureViewer.Infrastructure/Persistence/DatabaseInitializer.cs:104-121` — расширить `AchievementSeedItem` полем `Points`; на startup-секции идемпотентно `UPDATE` существующих записей, если `Points` разошёлся с каталогом.
- Новая EF миграция `AddAchievementPoints` (папка `backend/src/FutureViewer.Infrastructure/Persistence/Migrations/`).
- `backend/src/FutureViewer.Infrastructure/Persistence/Repositories/LeaderboardRepository.cs:17-97` — переписать `GetMonthlyAsync`/`GetAllTimeAsync`:
  - all-time: `FeedbackScore = SUM(AiScore WHERE Scored)`, `AchievementScore = SUM(Achievement.Points) по UserAchievements`, `TotalScore = FeedbackScore + AchievementScore`, сортировка по `TotalScore DESC`.
  - monthly: то же, но `UserAchievements` фильтруются по `UnlockedAt ∈ [from, to)`.
- `backend/src/FutureViewer.DomainServices/DTOs/LeaderboardEntryDto.cs` — добавить `FeedbackScore` и `AchievementScore` (оба int, обязательны); `TotalScore` остаётся обязательным.
- `backend/src/FutureViewer.DomainServices/DTOs/UserScoreSummaryDto.cs` — аналогично: `FeedbackScore`, `AchievementScore`.
- `frontend/src/types/index.ts` — добавить `feedbackScore` и `achievementScore` в `LeaderboardEntry` и `UserScoreSummary`.
- `frontend/src/components/LeaderboardTable.vue` — заголовок «Сумма» переименовать в «Итог»; под числом — маленькая подпись «★ {feedbackScore} / ✦ {achievementScore}».

**Подход:**
1. Очки: `first_reading=10, first_feedback=10, telegram_linked=10, streak_3=20, streak_7=50, streak_30=100, total_10=20, total_50=50, total_100=100, score_master=100, perfect_10=20, high_five=100`.
2. Миграция: `ALTER TABLE achievements ADD COLUMN points int NOT NULL DEFAULT 0;`. Значения проставит `DatabaseInitializer.SeedAchievementsAsync` при следующем старте (уже есть upsert-логика — проверить, что обновляет всё поле, а не только новые записи; если нет — расширить).
3. Репозиторий: два `GROUP BY` (по `ReadingFeedbacks` и `UserAchievements`), `FULL OUTER JOIN` в памяти через `LINQ` по `UserId`. Для `EF Core` + `Postgres` можно и один SQL, но LINQ-объединение проще поддерживать.
4. `LeaderboardEntryDto.TotalScore` = `FeedbackScore + AchievementScore`. Поле не убираем — фронт читает его для первичной сортировки.
5. Тесты:
   - `backend/tests/FutureViewer.Integration.Tests/Tests/LeaderboardEndpointTests.cs` (если нет — создать): «юзер только с ачивками, без фидбека, попадает в all-time лидерборд»; «monthly учитывает только ачивки unlocked в этом месяце».
   - Фронт: `frontend/tests/components/LeaderboardTable.spec.ts` — бейджи рендерятся.

**Верификация:**
- `dotnet test backend/FutureViewer.slnx`.
- Ручной тест: новый юзер → расклад → автоматически получает `first_reading` (10) → `/leaderboard?tab=alltime` → в таблице виден с `TotalScore=10`, подпись «★ 0 / ✦ 10».

---

## 5. Мобильная адаптация

**Файлы (по приоритету):**
1. `frontend/src/views/HomeView.vue` — заменить `grid-cols-3` на `grid-cols-1 sm:grid-cols-3` для пикера расклада (строки ~91–106).
2. `frontend/src/views/ReadingView.vue` + `frontend/src/composables/useSpread.ts` + `frontend/src/composables/useCardAnimation.ts` — масштабировать ширину карты (`140` → `clamp` от ширины контейнера), пересчитать `midY` offset от высоты контейнера, а не фиксированных 160 px.
3. `frontend/src/views/ResultView.vue` — `.cards-grid` gap сделать `gap: clamp(0.5rem, 2vw, 2rem)`; добавить `flex-wrap: wrap` и корректный `justify-content: center`.
4. `frontend/src/components/SiteHeader.vue` — добавить бургер-меню для `<640px`: при клике выдвигается панель со ссылками (Глоссарий/Лидерборд/История/Ачивки/Профиль/Админ/Выйти). Текущий hide без замены ломает навигацию.
5. `frontend/src/components/AdminUsersTable.vue`, `AdminFeedbacksTable.vue` — на `<768px` скрывать колонки `Admin`, `TG`, `Feedbacks`; либо стекать в card-layout через `@media (max-width: 768px) { tr { display: block; border: ...; } td { display: flex; justify-content: space-between; } }`.
6. `frontend/src/components/LeaderboardTable.vue` — аналогично: на `<640px` скрыть `feedbackCount` и `averageScore`, оставив ранг/имя/итог.

**Подход:** mobile-first через Tailwind-префиксы (`sm:`, `md:`) + точечный CSS для таблиц. Существующий viewport meta тег уже корректный (`width=device-width, initial-scale=1.0`), Tailwind breakpoints дефолтные — не трогаем.

**Верификация:**
- Chrome DevTools Device Toolbar: iPhone SE (375×667), Pixel 7 (412×915), iPad (768×1024).
- Пройти сценарий: логин → HomeView → ReadingView (анимация не должна вылазить за экран) → ResultView → LeaderboardView → AchievementsView → AdminView (на iPad+).
- На iPhone SE: никакого горизонтального скролла, все тексты читаемы, навигация доступна через бургер.

---

## 6. «Мы учимся на ваших откликах» в «О нас»

**Файлы:**
- `frontend/src/components/SiteFooter.vue` — модалка «О нас», строки 21–45.

**Подход:** между последним параграфом истории и закрывающей фразой «Доверься картам…» (строка 42–44) вставить новый `<p>`:

> «Наш ИИ становится точнее благодаря вам: мы анализируем ваши отклики на расклады и используем их как обучающий сигнал, чтобы интерпретации в будущем лучше отражали реальность. Никаких персональных данных — только ощущения от предсказаний.»

Формулировку согласовать с пользователем (в текущем плане — черновик).

**Верификация:** открыть футер → «О нас» → прочитать новый абзац; проверить, что не ломает вёрстку на мобильном.

---

## 7. Русификация сайта и названий раскладов

**Файлы:**
- `frontend/index.html:12` — `<title>`.
- `frontend/src/components/SiteHeader.vue:59` — логотип.
- `frontend/src/views/HomeView.vue:74` — `✦ FUTURE VIEWER ✦` → русский аналог.
- `frontend/src/components/SiteFooter.vue:11` — футер.
- `backend/src/FutureViewer.Infrastructure/Payment/YukassaClient.cs:62` — описание платежа.
- `backend/src/FutureViewer.Infrastructure/Telegram/TelegramUpdateHandler.cs:43` — сообщение бота уже на русском, но упоминает «Future Viewer» — заменить.
- Названия раскладов уже на русском (`Spread.cs`) — проверить, что `SpreadType` enum-метки в админке/логах тоже переведены, если где-то показываются пользователю.

**Подход:**
1. Новое русское название: **«Вуаль Грядущего»**.
2. Глобально заменить «Future Viewer» → «Вуаль Грядущего» в UI-файлах:
   - `frontend/index.html:12` → `<title>Вуаль Грядущего · ТАРО</title>`
   - `frontend/src/components/SiteHeader.vue:59` → `<span class="logo-text gold-text">Вуаль Грядущего</span>`
   - `frontend/src/views/HomeView.vue:74` → `✦ ВУАЛЬ ГРЯДУЩЕГО ✦`
   - `frontend/src/components/SiteFooter.vue:11` → заменить
   - `backend/src/FutureViewer.Infrastructure/Payment/YukassaClient.cs:62` → `$"Подписка «Вуаль Грядущего» для {userEmail}"`
   - `backend/src/FutureViewer.Infrastructure/Telegram/TelegramUpdateHandler.cs:43` → «…откройте приложение «Вуаль Грядущего» и нажмите…»
3. Репозиторий/код/пакеты — **не переименовывать** (ломать namespace `FutureViewer.*` не надо, это внутреннее имя). Меняем только пользовательские строки.
4. Пройтись по всем `.vue` и проверить, что нет английских вкраплений в UI (labels, placeholders, alt-тексты).

**Верификация:** `grep -r "Future Viewer" frontend/src` возвращает пусто (кроме комментариев/конфигов, где допустимо); пройти по всем страницам и убедиться, что нет английских слов в UI (кроме намеренных — названия колод, типографские элементы вроде `◆`).

---

## 8. Telegram: убрать вебхуки, оставить только long polling

**Файлы:**
- `backend/src/FutureViewer.Host/Endpoints/TelegramEndpoints.cs:61-101` — **удалить** endpoint `POST /api/telegram/webhook` и все константы (`SecretTokenHeader`, `WebhookMaxBytes`, `UpdateJsonOptions`); убрать `using` для `System.Security.Cryptography`, `System.Text`, `System.Text.Json`, `Microsoft.Extensions.Options`, `Telegram.Bot`, `Telegram.Bot.Polling`, `Telegram.Bot.Types`.
- `backend/src/FutureViewer.Infrastructure/Telegram/TelegramOptions.cs:9-10` — удалить `WebhookUrl` и `SecretToken`.
- `backend/src/FutureViewer.Infrastructure/Telegram/TelegramPollingService.cs:38-42` — удалить ветку «если вебхук задан — не стартуем». Теперь polling всегда стартует при `IsEnabled`.
- `backend/src/FutureViewer.Host/appsettings.json:39-40` — убрать `WebhookUrl` и `SecretToken`.
- `docker-compose.yml:38-39` — убрать env `Telegram__WebhookUrl` и `Telegram__SecretToken`; из `.env`-примера (если есть) тоже убрать.
- `CLAUDE.md` — обновить раздел о Telegram: удалить упоминания вебхука и `TELEGRAM_SECRET_TOKEN`/`TELEGRAM_WEBHOOK_URL`, оставить только polling.
- `backend/tests/FutureViewer.Integration.Tests/Tests/TelegramEndpointTests.cs` — удалить тесты webhook endpoint (или весь файл, если там только они).
- На старте приложения (один раз): если бот был настроен на вебхук ранее, выполнить `DeleteWebhookAsync` перед стартом polling (иначе Telegram API вернёт conflict). Добавить этот вызов в `TelegramPollingHostedService.ExecuteAsync` перед `client.ReceiveAsync`.

**Подход:**
1. Удалить вебхук-эндпоинт и все связанные типы.
2. В `TelegramPollingHostedService`: добавить `await client.DeleteWebhookAsync(dropPendingUpdates: true, cancellationToken: stoppingToken);` перед `ReceiveAsync` — это идемпотентно и гарантирует, что polling не столкнётся с зависшим webhook-ом на Telegram-стороне.
3. Подчистить конфиги и env.
4. Обновить `CLAUDE.md`: в описании Telegram-подсистемы вычеркнуть вебхук и `SecretToken`.

**Верификация:**
- `dotnet build` — зелёный, неиспользуемые `using` убраны.
- `dotnet test backend/FutureViewer.slnx` — проходит; удалённые тесты webhook не нужны.
- Локально: `docker compose up --build`, привязать Telegram в UI, отправить `/start <token>` боту — логи показывают `Starting Telegram long polling` и корректную обработку.

---

## 9. Подтверждение email, восстановление пароля, саппорт-почта в конфиге

### 9a. Saппорт-почта в конфиге + в футере

**Файлы:**
- `backend/src/FutureViewer.Host/appsettings.json` — новая секция `"Support": { "Email": "support@vualgryaduschego.ru" }`.
- `docker-compose.yml` — env `Support__Email: ${SUPPORT_EMAIL:-support@vualgryaduschego.ru}`.
- Новый публичный endpoint `GET /api/public/config` в `backend/src/FutureViewer.Host/Endpoints/` — возвращает `{ supportEmail }`, без авторизации, кэшируется.
- `frontend/src/api/publicApi.ts` (новый) — клиент; `frontend/src/stores/usePublicConfigStore.ts` — загружается один раз при старте.
- `frontend/src/components/SiteFooter.vue` — внизу перед/после копирайта добавить строку: `Связаться с нами: <a href="mailto:{supportEmail}">{supportEmail}</a>`.

**Подход:** конфиг в `appsettings.json`, читается через `IOptions<SupportOptions>`; endpoint возвращает JSON. Фронт грузит на монтировании `App.vue`, хранит в Pinia, SiteFooter показывает. Подтверждение этой почты **не требуется** — она не привязана к пользователю, просто контакт.

### 9b. Подтверждение email при регистрации

**Файлы:**
- `backend/src/FutureViewer.Domain/Entities/User.cs` — добавить:
  - `bool IsEmailVerified { get; set; } = false;`
  - `string? EmailVerificationToken { get; set; }`
  - `DateTime? EmailVerificationSentAt { get; set; }`
- EF миграция `AddEmailVerification`.
- `backend/src/FutureViewer.DomainServices/Interfaces/IEmailSender.cs` (новый) — `SendAsync(to, subject, htmlBody, ct)`.
- `backend/src/FutureViewer.Infrastructure/Email/SmtpEmailSender.cs` (новый) — реализация через `MailKit` (пакет `MailKit` в `FutureViewer.Infrastructure.csproj`).
- `backend/src/FutureViewer.Infrastructure/Email/EmailOptions.cs` — `Host`, `Port`, `Username`, `Password`, `From`, `UseSsl`, `FrontendUrl` (для ссылок в письмах).
- `backend/src/FutureViewer.Host/appsettings.json` + `docker-compose.yml` — секция `"Email": { ... }` / env `Email__Host`, `Email__Port`, `Email__Username`, `Email__Password`, `Email__From`, `Email__FrontendUrl`.
- `backend/src/FutureViewer.DomainServices/Services/AuthService.cs`:
  - `RegisterAsync`: генерит `EmailVerificationToken` (32 байта, base64url), сохраняет `EmailVerificationSentAt=utcnow`, вызывает `_email.SendAsync(email, "Подтверждение регистрации", html с ссылкой `{FrontendUrl}/verify-email?token=...`)`. JWT не выдаётся.
  - Новый `VerifyEmailAsync(token, ct)`: находит юзера по токену, проверяет что не истёк (24 часа), ставит `IsEmailVerified=true`, очищает токен, возвращает `AuthResponse` (JWT).
  - `LoginAsync`: если `IsEmailVerified=false` — бросать новое исключение `EmailNotVerifiedException` (`403`). На фронте — показать кнопку «Отправить повторно».
  - Новый `ResendVerificationAsync(email, ct)`: троттлинг — не чаще раза в 60 сек (проверка по `EmailVerificationSentAt`).
- `backend/src/FutureViewer.Host/Endpoints/AuthEndpoints.cs`:
  - `POST /api/auth/verify-email` — `{ token }` в теле.
  - `POST /api/auth/resend-verification` — `{ email }` в теле.
  - `POST /api/auth/register` — теперь возвращает `202 Accepted` c `{ userId, email, verificationRequired: true }`, без JWT.
- `frontend/src/views/RegisterView.vue` — после успешной регистрации показать «Мы отправили письмо на {email}. Перейдите по ссылке, чтобы подтвердить».
- `frontend/src/views/VerifyEmailView.vue` (новый) — роут `/verify-email`, читает `?token=`, вызывает `authApi.verifyEmail(token)`, при успехе логинит юзера.
- `frontend/src/views/LoginView.vue` — при ошибке `EmailNotVerified` показать кнопку «Отправить письмо повторно».
- `frontend/src/api/authApi.ts` — `verifyEmail(token)`, `resendVerification(email)`.
- `frontend/src/router/index.ts` — добавить роут `/verify-email`.

### 9c. Восстановление пароля

**Файлы:**
- `backend/src/FutureViewer.Domain/Entities/User.cs`:
  - `string? PasswordResetToken { get; set; }`
  - `DateTime? PasswordResetTokenExpiresAt { get; set; }`
- Миграция `AddPasswordReset` (можно объединить с `AddEmailVerification` в одну миграцию `AddAuthTokens`).
- `backend/src/FutureViewer.DomainServices/Services/AuthService.cs`:
  - `ForgotPasswordAsync(email, ct)`: если юзер существует — генерит токен (действителен 1 час), шлёт письмо с `{FrontendUrl}/reset-password?token=...`. Всегда возвращает `204` (не раскрываем наличие юзера — защита от user enumeration).
  - `ResetPasswordAsync(token, newPassword, ct)`: проверка токена + истечения → пересчитать hash, очистить токен, опционально логинить.
- `backend/src/FutureViewer.Host/Endpoints/AuthEndpoints.cs`:
  - `POST /api/auth/forgot-password` — `{ email }`.
  - `POST /api/auth/reset-password` — `{ token, newPassword }`.
- `frontend/src/views/ForgotPasswordView.vue` (новый) — `/forgot-password`, форма с email.
- `frontend/src/views/ResetPasswordView.vue` (новый) — `/reset-password?token=...`, форма с новым паролем + подтверждением.
- `frontend/src/views/LoginView.vue` — добавить ссылку «Забыли пароль?» под формой.
- `frontend/src/router/index.ts` — роуты `/forgot-password`, `/reset-password`.
- `frontend/src/api/authApi.ts` — `forgotPassword(email)`, `resetPassword(token, newPassword)`.

### 9d. Общие соображения

- **Email-шаблоны**: простые inline-HTML в коде сервиса (3 письма: verify, resend, reset). Вынести в `backend/src/FutureViewer.Infrastructure/Email/Templates/` как embedded resources, если объём вырастет. Пока — inline.
- **Безопасность токенов**: 32 байта случайных → `Base64Url`. Хранятся в открытом виде в БД (допустимо для одноразовых токенов с коротким TTL; альтернатива — хранить hash, но усложняет поиск — отложим).
- **Валидация паролей** в `ResetPasswordRequestValidator`: тот же набор правил, что в `RegisterRequestValidator` (переиспользовать).
- **Локальная разработка**: если `Email:Host` пуст — `SmtpEmailSender` логирует письмо в консоль вместо отправки (удобно для dev). Конфиг пустой по умолчанию в `appsettings.json`.
- **Тесты**:
  - `backend/tests/FutureViewer.DomainServices.Tests/AuthServiceTests.cs` — кейсы: register → verify → login; login без verify → 403; resend-throttle; reset-password с истёкшим токеном.
  - `backend/tests/FutureViewer.Integration.Tests/Tests/AuthEndpointTests.cs` — `MockEmailSender`, прогоняем полный цикл.

**Верификация (вручную):**
- Зарегистрироваться → получить письмо (в dev — в логе) → открыть ссылку → стать подтверждённым → залогиниться.
- Нажать «Забыли пароль?» → получить письмо → открыть ссылку → задать новый пароль → залогиниться с новым.
- Открыть любую страницу → в футере видна `<a href="mailto:support@...">`.

---

## Порядок работы и критические файлы

**Рекомендованный порядок мержа** (от безопасного к рискованному):

1. **Пункт 8** (Telegram webhook → polling only) — изолированное удаление, быстро валидируется.
2. **Пункт 1** (Лицензия) — только текстовый файл.
3. **Пункт 6** (строка в «О нас»).
4. **Пункт 9a** (саппорт-почта в конфиге и футере) — небольшая изолированная фича.
5. **Пункт 3** (lucide-иконки ачивок).
6. **Пункт 7** (русификация → «Вуаль Грядущего»).
7. **Пункт 2** (описания колод и раскладов в глоссарии + на главной).
8. **Пункт 4** (ачивки → рейтинг) — миграция БД, новый расчёт рейтинга.
9. **Пункт 9b + 9c** (подтверждение email + восстановление пароля) — миграция, SMTP-клиент, новые вьюхи и эндпоинты. Это самая крупная часть; лучше разбить на два отдельных PR (verify, затем reset).
10. **Пункт 5** (мобилка) — широкая вёрстка, ревью «на глаз» по страницам.

**Сквозная верификация после всего:**
- `dotnet test backend/FutureViewer.slnx`
- `cd frontend && npm run build && npm test`
- `docker compose up --build` + ручной проход: логин → создать расклад → feedback → ачивки → лидерборд → профиль → админ; на iPhone SE viewport; Telegram-бот отвечает.

---

## Решённые вопросы

- Название сайта: **Вуаль Грядущего**.
- Пункт 2: делаем **и колоды, и расклады** — два раздела в глоссарии + соответствующие блоки на HomeView.
- Пункт 4: **сумма очков ачивки + фидбек → общий рейтинг**; monthly учитывает только ачивки, открытые в этом месяце.
- Пункт 3: **lucide-vue-next** (не SVG вручную, не эмодзи).

## Остающиеся открытые вопросы (можно утвердить в процессе)

1. **Лицензия.** Формулировка «source-available + 20% gross revenue share для commercial use» — черновик напишу на основе PolyForm Shield 1.0.0 + Commons Clause; контакт для коммерческих запросов — `utre43@gmail.com`. Можно изменить после review.
2. **Саппорт-почта в конфиге.** Значение по умолчанию в `docker-compose.yml` — `support@vualgryaduschego.ru` (плейсхолдер, пользователь может заменить на реальную).
3. **SMTP-провайдер.** В `.env` подставим пример SMTP (например, `smtp.yandex.ru:465` с SSL) — пользователь заменит credentials.
4. **Формулировка «обучаемся на фидбеке»** в «О нас» — черновик выше, утвердить/переписать.
