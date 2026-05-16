# План: плавный фронт и аудит анимаций

## Summary
- Цель: убрать лаги и "зависания" во время расклада, с приоритетом плавности над декоративными эффектами.
- Объем: полный фронтовый аудит анимаций и дорогих визуальных эффектов, но без деплоя.
- После выхода из Plan Mode сначала сохранить этот план в `plans/frontend-animation-performance-audit.md`, затем делать правки.

## Key Changes
- Ввести route-aware performance mode: на `/reading` и во время streaming на `/result` приглушать или останавливать тяжелые фоновые эффекты.
- Оптимизировать `Starfield` и `MistLayer`: cap для DPR, меньше работы на кадр, cleanup GSAP, pause/disable на performance-critical экранах, учет `prefers-reduced-motion`.
- Переписать расклад карт так, чтобы во время deal двигались легкие "рубашки", а полноценные `CardFlip` с изображениями подключались только после preload/decode карт.
- Добавить preload/decode реальных карт перед flip, чтобы браузер не декодировал JPG прямо в момент переворота.
- Управлять GSAP lifecycle явно: хранить timeline refs, kill на unmount/restart, очищать `will-change`, добавить `AbortController` для stream-запроса.
- Сгладить streaming/render нагрузку: буферизовать chunk updates в `useReadingStore`, throttle markdown parsing/autoscroll в `ResultView`.
- Пройтись по найденным дорогим CSS-паттернам (`transition: all`, тяжелые blur/shadow/filter) и заменить их на явные, более дешевые свойства там, где это влияет на интерактивность.

## Interfaces
- Backend/public API не менять.
- Frontend internal API:
  - расширить `useReadingStore.createStream(...)` поддержкой `AbortSignal`;
  - оставить `readingApi.createStream(..., signal)` как низкоуровневый transport;
  - добавить внутренний motion/performance composable или props для `Starfield`/`MistLayer`.

## QA / Verification
- Automated checks: `cd frontend && npm run type-check`, `npm test`, `npm run build`.
- Rendered browser QA через Browser Use/in-app browser на локальном Vite:
  - desktop около `1440x900`;
  - mobile около `390x844`;
  - проверить Home → Reading → Result, включая Celtic Cross как самый тяжелый расклад.
- Если локальный backend/OpenAI stream недоступен, точный stream-flow покрыть безопасной mocked проверкой через тест/Playwright route interception, а Browser Use использовать для визуальной проверки экранов и отсутствия очевидных лагов/layout overlap.
- После правок запустить локальный dev server и дать URL, чтобы ты сам потыкался до решения о деплое.

## Deploy
- Деплой не делать.
- Не пушить, не мержить и не обновлять production, пока ты отдельно не скажешь деплоить.
