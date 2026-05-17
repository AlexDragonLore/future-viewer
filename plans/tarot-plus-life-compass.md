# Tarot+ Life Compass MVP

## Summary
- Add Tarot+ as a separate one-time paid product: 100 ₽ per session, independent from Future Viewer Pro subscription.
- Do not deploy, merge to `main`, or push release changes without explicit approval. After implementation, run locally and give the local URL so the user can try it first.
- First implementation step after leaving Plan Mode: save this approved plan to `plans/tarot-plus-life-compass.md`.

## Backend Changes
- Add `TarotPlusSessionStatus`, `TarotPlusRoute`, and `TarotPlusSession`; wire EF `DbSet`, configuration, repository, migration, and `User.TarotPlusSessions`.
- Add `TarotPlusService` with preview, payment, intake answers, next step, report generation, follow-up, history, and get-by-id methods.
- Store preview answers, intake answers, selected spreads, drawn cards, safety flags, report, follow-ups, payment id, price, paid time, and expiration on the session.
- Use a separate Tarot+ spread catalog, not `SpreadType`; draw all cards backend-side with `CardDeckService`, draw once per session, and reuse saved `DrawnCardsJson`.
- Use 3 preview inputs plus 5-9 paid interview answers, for 8-12 total user answers before report generation.
- Add `ITarotPlusAI` and `TarotPlusAI` using a separate `TarotPlusAI` config section defaulting to DeepSeek `deepseek-v4-pro`, with JSON output for routing/next-question responses.
- Keep existing normal readings on the existing AI config; Tarot+ gets its own client/options so V4 Pro does not raise cost for ordinary readings.
- Add authenticated endpoints under `/api/tarot-plus/*`.

## Payment Changes
- Add `PaymentProductType` and `PaymentCreateRequest`; keep `CreateSubscriptionPaymentAsync` as a compatibility wrapper over `CreatePaymentAsync`.
- Extend webhook and verification models with `ProductType` and `TarotPlusSessionId`; trust verified provider metadata over raw webhook metadata.
- Move webhook orchestration into `PaymentWebhookService`: parse, verify paid+succeeded, record duplicate protection, then route subscription payments to `SubscriptionService` and Tarot+ payments to `TarotPlusService`.
- Extend ЮKassa/YooMoney providers for product-specific amount checks: subscription 300 ₽, Tarot+ 100 ₽.
- Use Tarot+ payment labels/metadata:
  - ЮKassa: `product_type=tarot_plus_session`, `user_id`, `tarot_plus_session_id`.
  - YooMoney: `fv:tp:{userId}:{sessionId}:{suffix}`.
- Payment return URL for Tarot+ will go directly to `/tarot-plus/{sessionId}`.

## Frontend Changes
- Add Tarot+ types, `tarotPlusApi`, and `useTarotPlusStore`.
- Add routes:
  - `/tarot-plus`
  - `/tarot-plus/:id`
- Build `TarotPlusView.vue` for product intro, price 100 ₽, three preview questions, preview result, and payment CTA.
- Build `TarotPlusSessionView.vue` for payment-pending, intake, report-generating, report-ready, follow-up, and completed states.
- Render reports/follow-up answers with `marked`, matching existing AI markdown rendering.
- Add a Tarot+ CTA on `HomeView` and authenticated header/burger navigation links.
- Update `LegalView.vue` with Tarot+ product/storage/disclaimer language. There is no current `PrivacyView.vue`, so privacy text stays in the existing legal page unless a separate privacy route is requested later.
- Do not add an unused `frontend/src/seo/routes.json`; current repo has no SEO registry. Add route `meta` for title/description/noindex and wire metadata only if the route metadata is consumed in the router.

## QA / Verification
- Backend:
  - `dotnet test backend/FutureViewer.slnx`
  - If full suite is slow: `dotnet test backend/tests/FutureViewer.DomainServices.Tests/FutureViewer.DomainServices.Tests.csproj` and Tarot+ integration tests only.
- Frontend:
  - `cd frontend && npm run type-check`
  - `cd frontend && npm test`
  - `cd frontend && npm run build`
- Browser QA with the in-app Browser Use tool:
  - Open local `/tarot-plus`.
  - Verify price 100 ₽ and preview flow.
  - Verify payment redirect creation without completing real production payment.
  - Open `/tarot-plus/:id`, test paid/intake/report/follow-up states using local test or stubbed payment flow.
  - Check desktop and mobile viewport layout.
- No production verification and no deploy until explicit approval.

## Assumptions
- Initial MVP had no admin Tarot+ page; the addendum below supersedes this for the current implementation.
- Report generation is synchronous in the API call; on AI failure the session returns from `ReportGenerating` to a retryable pre-report state.
- DeepSeek model/config is based on current official docs: model `deepseek-v4-pro`, OpenAI-compatible base URL `https://api.deepseek.com`, JSON Output support, 1M context, and pricing/model metadata from [DeepSeek First API Call](https://api-docs.deepseek.com/), [Models & Pricing](https://api-docs.deepseek.com/quick_start/pricing), and [JSON Output](https://api-docs.deepseek.com/guides/json_mode).

## Addendum: Admin + Subscription Bonus
- Add Tarot+ to the admin panel: list/filter sessions by user/status, show Tarot+ counts in stats, show recent Tarot+ sessions and available bonus credits on user detail.
- Add admin support action for editing a user's Tarot+ credits.
- On every successful verified subscription payment, grant one Tarot+ credit to the user.
- When a user starts payment for a Tarot+ preview and has credits, consume one credit and mark that session paid without creating an external payment.
- QA additions:
  - Backend tests for subscription credit grant, credit consumption, and admin Tarot+ endpoints.
  - Frontend type-check/tests for admin Tarot+ state and UI.
  - Browser QA should include `/admin/tarot-plus` and the user detail Tarot+ credits section when a local authenticated admin session is available.
