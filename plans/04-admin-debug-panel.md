# Admin / Debug Panel

## Context

Future Viewer has grown from a single-view tarot reader into a gamified system: subscriptions, feedback scoring (which drives leaderboard points), achievements, and Telegram deep links. During development we have no practical way to manipulate this data without writing SQL or waiting for real events (e.g. the 24h feedback delay, a real Yandex.Kassa payment, or an AI scoring pass). We need a first-party admin panel so a developer can: issue feedbacks on demand, edit `AiScore` (= award points), grant/extend subscriptions, and inspect/adjust users.

Goals:
- Unblock debug workflows (no more `psql` for routine state changes).
- Stay schema-lean: no new "points" table — points stay derived from `ReadingFeedback.AiScore`.
- Add admin authorization without disturbing the existing `.RequireAuthorization()` pattern used everywhere else.

Non-goals:
- No audit-log table (logger is enough for a single-developer tool).
- No impersonation (declined — safer without it; can be added later).
- No admin-specific theme/UI framework — reuse `.mystic-card` and the `LeaderboardTable.vue` patterns.

## Authorization model

### Domain + migration
- Add `public bool IsAdmin { get; set; } = false;` to `backend/src/FutureViewer.Domain/Entities/User.cs`.
- Configure in `backend/src/FutureViewer.Infrastructure/Persistence/Configurations/UserConfiguration.cs` as column `is_admin`, non-null, default `false`.
- EF migration `AddIsAdminToUser` under `backend/src/FutureViewer.Infrastructure/Persistence/Migrations/`.

### JWT role claim
- In `backend/src/FutureViewer.Infrastructure/Auth/JwtTokenService.cs` (`CreateAccessToken`), add `new Claim(ClaimTypes.Role, "Admin")` when `user.IsAdmin`. Keep `sub`/`email`/`jti` intact.

### Authorization policy
- In `backend/src/FutureViewer.Host/Program.cs` replace `AddAuthorization()` with `AddAuthorization(opt => opt.AddPolicy("Admin", p => p.RequireRole("Admin")))`.
- The new admin group uses `.RequireAuthorization("Admin")`.

### Bootstrap: config-driven seeding
- Add `"Admin": { "Emails": ["utre43@gmail.com"] }` to `appsettings.json` / `appsettings.Development.json`.
- Extend `backend/src/FutureViewer.Infrastructure/Persistence/DatabaseInitializer.cs` with `SeedAdminsAsync(AppDbContext, IConfiguration, CancellationToken)` called after the achievements seed. For each listed email (case-insensitive match) that exists, set `IsAdmin = true` if not already. Non-existent emails are silently skipped (admin can register later and be promoted on next startup, or via the panel).
- Update the `DatabaseInitializer` call site in `Program.cs` to pass `IConfiguration` through the scope.

### Auth response + frontend awareness
- Add `IsAdmin` to `backend/src/FutureViewer.DomainServices/DTOs/AuthResponse.cs` and set it in `AuthService.LoginAsync` / `RegisterAsync`.
- `frontend/src/stores/useAuthStore.ts`: persist `fv_is_admin` alongside token/email; expose `isAdmin` computed.
- `frontend/src/router/index.ts`: add `requiresAdmin` meta handling in `beforeEach` (redirect non-admins to `/`).
- `frontend/src/components/SiteHeader.vue`: conditional link `<RouterLink v-if="auth.isAdmin" to="/admin" data-testid="nav-admin">Админ</RouterLink>`.

## Backend admin endpoints

All under `app.MapGroup("/api/admin").WithTags("Admin").RequireAuthorization("Admin")` in a new file `backend/src/FutureViewer.Host/Endpoints/AdminEndpoints.cs` wired via `app.MapAdmin()` in `Program.cs`.

Orchestration lives in a new `backend/src/FutureViewer.DomainServices/Services/AdminService.cs` (registered in `DomainServicesExtensions`). It composes existing services and the small repository additions listed below. No EF leaks into DomainServices — additions go to existing repos.

### Repository additions
- `IUserRepository`: `SearchAsync(string? email, int skip, int take, CancellationToken)`, `CountAsync(string? email, CancellationToken)`, `DeleteAsync(Guid id, CancellationToken)`.
- `IFeedbackRepository`: `DeleteAsync(Guid id, CancellationToken)`, `SearchAsync(Guid? userId, FeedbackStatus?, int skip, int take, CancellationToken)`, `CountAsync(Guid? userId, FeedbackStatus?, CancellationToken)`.
- `IReadingRepository`: `CountAsync(CancellationToken)`, `CountSinceAsync(DateTime from, CancellationToken)`, `GetByUserAsync(Guid userId, int take, CancellationToken)` if not already present.
- `IAchievementRepository`: `RevokeAsync(Guid userId, Guid achievementId, CancellationToken)`, `GetByCodeAsync(string code, CancellationToken)`.

### Users

| Verb + path | Body | Response |
|---|---|---|
| `GET /api/admin/users?search=&page=1&pageSize=20` | — | `{ items: AdminUserListItem[], total }` |
| `GET /api/admin/users/{id}` | — | `AdminUserDetailDto` (user + latest 20 readings, latest 20 feedbacks, achievements, telegram status) |
| `PUT /api/admin/users/{id}/admin` | `{ isAdmin: bool }` | `AdminUserListItem` |
| `DELETE /api/admin/users/{id}` | — | 204 |

`AdminUserListItem` fields: `id, email, createdAt, isAdmin, subscriptionStatus, subscriptionExpiresAt, telegramChatId, totalReadings, totalFeedbacks, totalScore`.

Cascade: before deleting a User, `AdminService.DeleteUserAsync` opens a transaction and explicitly removes child rows in order (`UserAchievement`, `ReadingFeedback`, `Reading` + `ReadingCard`), unless the existing EF configurations already cascade — verify in `ReadingFeedbackConfiguration.cs` and `UserAchievementConfiguration.cs` first and fall back to explicit delete where missing. Admin cannot delete themselves (compare `ctx.User.GetUserId()` with target id).

### Subscriptions

| Verb + path | Body | Response |
|---|---|---|
| `PUT /api/admin/users/{id}/subscription` | `{ status: "None"\|"Active"\|"Expired"\|"Cancelled", expiresAt?: ISO }` | `SubscriptionStatusDto` |

Writes `SubscriptionStatus` and `SubscriptionExpiresAt` directly. Convenience default: if `status=Active` and no `expiresAt`, use `UtcNow + 30 days`. Returns via existing `SubscriptionService.GetStatusAsync`.

### Feedbacks ("issue feedbacks" + "award points")

| Verb + path | Body | Response |
|---|---|---|
| `GET /api/admin/feedbacks?userId=&status=&page=1&pageSize=20` | — | `{ items: AdminFeedbackDto[], total }` |
| `POST /api/admin/feedbacks` | `{ readingId, scheduledAt?: ISO, bypassDelay?: bool, replace?: bool }` | `AdminFeedbackDto` |
| `POST /api/admin/feedbacks/synthetic` | `{ readingId, aiScore, aiScoreReason?, isSincere?: true, selfReport? }` | `AdminFeedbackDto` |
| `PUT /api/admin/feedbacks/{id}` | `{ aiScore?, aiScoreReason?, isSincere?, status?, selfReport?, scheduledAt?, notifiedAt?, answeredAt? }` | `AdminFeedbackDto` |
| `DELETE /api/admin/feedbacks/{id}` | — | 204 |
| `POST /api/admin/feedbacks/run-notifications` | — | `{ processed: int }` |

Implementation notes:
- `POST /api/admin/feedbacks` wraps `FeedbackService.ScheduleAsync` but when `bypassDelay=true` overrides `ScheduledAt = UtcNow - 1min` so `FeedbackNotificationJob` picks it up on next tick. `replace=true` deletes an existing feedback for the reading first (respects any unique index on `ReadingId` — verify in `ReadingFeedbackConfiguration.cs`).
- `POST /api/admin/feedbacks/synthetic` creates a `ReadingFeedback` directly in status `Scored`, `AnsweredAt=UtcNow`, `IsSincere=true` by default. Validates that `readingId` belongs to a real user; `AdminFeedbackDto` carries `userId` derived from the reading.
- `PUT /api/admin/feedbacks/{id}` applies only the provided fields. After commit calls `AchievementService.CheckAndGrantAsync(feedback.UserId)`.
- `run-notifications` triggers a single batch pass. Extract the body of `FeedbackNotificationJob.ProcessBatchAsync` into a new `backend/src/FutureViewer.Infrastructure/BackgroundServices/FeedbackNotificationProcessor.cs` so both the hosted service and the admin endpoint call the same code path.

"Award points" maps to these endpoints: edit an existing feedback's `AiScore` (common path) or create a synthetic Scored feedback (when no answerable feedback exists yet). No `PointAdjustments` table.

### Achievements

| Verb + path | Body | Response |
|---|---|---|
| `POST /api/admin/users/{id}/achievements` | `{ code: string }` | `AchievementDto` |
| `DELETE /api/admin/users/{id}/achievements/{code}` | — | 204 |
| `POST /api/admin/users/{id}/achievements/recheck` | — | `AchievementDto[]` (newly granted) |

### Telegram

| Verb + path | Body | Response |
|---|---|---|
| `DELETE /api/admin/users/{id}/telegram` | — | 204 |
| `PUT /api/admin/users/{id}/telegram` | `{ chatId: long }` | `{ linked: true, chatId }` |

Unlink reuses `TelegramLinkService.UnlinkAsync`. Setting `chatId` writes directly on `User` and clears `TelegramLinkToken`; catch unique-index violations and surface as 409.

### Stats

| Verb + path | Body | Response |
|---|---|---|
| `GET /api/admin/stats` | — | `{ totalUsers, adminCount, activeSubscriptions, readingsToday, readingsThisWeek, pendingFeedbacksToNotify, scoredFeedbacksThisMonth }` |

Implemented with a small set of aggregate methods on the existing repos (keeps DomainServices EF-free).

### Audit logging
Every `AdminService` mutation logs at `Information` via `ILogger<AdminService>` with `actorUserId`, `actorEmail`, `action`, `targetId`, and a minimal change summary. No DB audit table.

## Frontend admin UI

### New files
- `frontend/src/types/admin.ts` — TS DTOs mirroring backend.
- `frontend/src/api/adminApi.ts` — one function per endpoint over `httpClient`.
- `frontend/src/stores/useAdminStore.ts` — Pinia store: paginated users list, selected user detail, paginated feedbacks, stats. Per-section `loading`/`error`.
- `frontend/src/views/AdminView.vue` — shell with sub-nav (tabs).
- `frontend/src/views/admin/AdminUsersView.vue`
- `frontend/src/views/admin/AdminFeedbacksView.vue`
- `frontend/src/views/admin/AdminStatsView.vue`
- `frontend/src/components/admin/AdminUsersTable.vue` — reuses `LeaderboardTable.vue` styling.
- `frontend/src/components/admin/AdminUserDetailDrawer.vue` — subscription, admin toggle, achievements grant/revoke/recheck, telegram set/unlink, delete.
- `frontend/src/components/admin/AdminFeedbacksTable.vue` — inline-edit `aiScore` (1–10), status select, `isSincere` toggle, save/delete per row.
- `frontend/src/components/admin/AdminCreateFeedbackForm.vue` — modes: schedule / immediate / synthetic-scored; reading picker by id.
- `frontend/src/components/admin/AdminStatTile.vue` — KPI tile.

### Router
Nested under `/admin` with `meta: { requiresAuth: true, requiresAdmin: true }` on the parent:

```
/admin → redirect /admin/users
/admin/users, /admin/users/:id
/admin/feedbacks
/admin/stats
```

### UX details
- Users tab: debounced email search (300 ms), server-side pagination (20/page), row click opens detail drawer. Destructive ops (delete user, revoke achievement) show a confirmation modal.
- Feedbacks tab: filters by user + status, inline edit saves per row, "Создать фидбек" opens the form with reading picker.
- Stats tab: 2×4 KPI grid with a manual refresh button.
- Nav entry appears in `SiteHeader.vue` only for admins.

## Implementation order

### Task 1: Auth plumbing

- [x] Add `IsAdmin` to `User` entity + `UserConfiguration`
- [x] Create `AddIsAdminToUser` EF migration
- [x] Add `Admin` role claim to `JwtTokenService`
- [x] Add `IsAdmin` to `AuthResponse` + set in `AuthService` login/register
- [x] Add `Admin` authorization policy in `Program.cs`
- [x] Add `Admin:Emails` config + `SeedAdminsAsync` in `DatabaseInitializer`
- [x] Wire `DatabaseInitializer` to accept `IConfiguration`
- [x] Frontend: `useAuthStore` persists `fv_is_admin` + exposes `isAdmin` computed
- [x] Frontend: router `requiresAdmin` meta handling
- [x] Frontend: `SiteHeader` shows `Админ` link for admins
- [x] Frontend: `/admin` stub view
- [x] Tests + verification

### Task 2: Feedbacks + Points

- [x] `AdminService` skeleton + DI registration
- [x] `IFeedbackRepository` additions (`DeleteAsync`, `SearchAsync`, `CountAsync`)
- [x] `FeedbackNotificationProcessor` extraction from `FeedbackNotificationJob`
- [x] `POST/GET/PUT/DELETE /api/admin/feedbacks` + `/synthetic` + `/run-notifications`
- [x] Post-save `AchievementService.CheckAndGrantAsync`
- [x] Frontend: Feedbacks tab (`AdminFeedbacksView`, `AdminFeedbacksTable`, `AdminCreateFeedbackForm`)
- [x] Tests + verification

### Task 3: Subscriptions + Users list

- [x] `IUserRepository` additions (`SearchAsync`, `CountAsync`, `DeleteAsync`)
- [x] `IReadingRepository` additions (`CountAsync`, `CountSinceAsync`, `GetByUserAsync`)
- [x] `GET /api/admin/users`, `GET /api/admin/users/{id}`, `PUT /api/admin/users/{id}/admin`, `DELETE /api/admin/users/{id}`
- [x] `PUT /api/admin/users/{id}/subscription`
- [x] Frontend: Users tab + detail drawer
- [x] Tests + verification

### Task 4: Achievements + Telegram

- [x] `IAchievementRepository` additions (`RevokeAsync`, `GetByCodeAsync`)
- [x] `POST /api/admin/users/{id}/achievements`, `DELETE /api/admin/users/{id}/achievements/{code}`, `POST /api/admin/users/{id}/achievements/recheck`
- [x] `DELETE /api/admin/users/{id}/telegram`, `PUT /api/admin/users/{id}/telegram`
- [x] Frontend: wire buttons in detail drawer
- [x] Tests + verification

### Task 5: Stats

- [ ] `GET /api/admin/stats`
- [ ] Frontend: Stats tab with KPI tiles
- [ ] Tests + verification

## Risks / known limitations

- **JWT staleness after admin toggle.** Changing `IsAdmin` takes effect on next login; acceptable for debug use.
- **Score edits don't revoke achievements.** `CheckAndGrantAsync` only grants. Lowering a score below a threshold leaves stale achievements; use the explicit revoke endpoint for cleanup.
- **`ReadingFeedback.ReadingId` uniqueness.** Confirm in `ReadingFeedbackConfiguration.cs`. If unique, the "issue feedback" endpoint needs `replace=true` semantics; if not, duplicates coexist and admin can pick which to score.
- **`User.TelegramChatId` uniqueness.** Setting a chatId already linked elsewhere throws — surface 409 with a clear message.
- **User delete cascade.** Verify `ReadingFeedbackConfiguration` and `UserAchievementConfiguration` delete-behavior before relying on cascade; otherwise delete children explicitly inside a transaction.

## Verification

- `dotnet test backend/FutureViewer.slnx` (integration + unit suites).
- Manual backend smoke:
  - `POST /api/auth/login` as seeded admin → JWT contains `role=Admin`, response has `isAdmin=true`.
  - `curl -H "Authorization: Bearer …"` to each admin endpoint (users list, set subscription, create synthetic feedback, recheck achievements, stats) and check 200/204.
  - Non-admin token → 403 on the same paths.
- Frontend smoke: `npm run dev`, log in as admin, navigate `/admin`, for each tab: search → edit → save → confirm toast + data refresh.
- Telegram end-to-end: set own `TelegramChatId` via admin, `POST /api/admin/feedbacks` with `bypassDelay=true`, `POST /api/admin/feedbacks/run-notifications`, receive Telegram message at `{SiteUrl}/feedback/{token}`.
- Leaderboard recompute: edit a feedback's `AiScore`, reload `/leaderboard` → totals update.

## Critical files

**Modified**
- `backend/src/FutureViewer.Domain/Entities/User.cs`
- `backend/src/FutureViewer.Infrastructure/Persistence/Configurations/UserConfiguration.cs`
- `backend/src/FutureViewer.Infrastructure/Auth/JwtTokenService.cs`
- `backend/src/FutureViewer.Infrastructure/Persistence/DatabaseInitializer.cs`
- `backend/src/FutureViewer.Infrastructure/BackgroundServices/FeedbackNotificationJob.cs`
- `backend/src/FutureViewer.DomainServices/Services/AuthService.cs`
- `backend/src/FutureViewer.DomainServices/DTOs/AuthResponse.cs`
- `backend/src/FutureViewer.DomainServices/Interfaces/IUserRepository.cs`
- `backend/src/FutureViewer.DomainServices/Interfaces/IFeedbackRepository.cs`
- `backend/src/FutureViewer.DomainServices/Interfaces/IReadingRepository.cs`
- `backend/src/FutureViewer.DomainServices/Interfaces/IAchievementRepository.cs`
- `backend/src/FutureViewer.Host/Program.cs`
- `backend/src/FutureViewer.Host/appsettings.json`, `appsettings.Development.json`
- `frontend/src/stores/useAuthStore.ts`
- `frontend/src/router/index.ts`
- `frontend/src/components/SiteHeader.vue`

**New**
- `backend/src/FutureViewer.Infrastructure/Persistence/Migrations/*_AddIsAdminToUser.cs`
- `backend/src/FutureViewer.Infrastructure/BackgroundServices/FeedbackNotificationProcessor.cs`
- `backend/src/FutureViewer.DomainServices/Services/AdminService.cs`
- `backend/src/FutureViewer.DomainServices/DTOs/Admin/AdminUserListItem.cs`
- `backend/src/FutureViewer.DomainServices/DTOs/Admin/AdminUserDetailDto.cs`
- `backend/src/FutureViewer.DomainServices/DTOs/Admin/AdminFeedbackDto.cs`
- `backend/src/FutureViewer.DomainServices/DTOs/Admin/AdminStatsDto.cs`
- `backend/src/FutureViewer.Host/Endpoints/AdminEndpoints.cs`
- `frontend/src/types/admin.ts`
- `frontend/src/api/adminApi.ts`
- `frontend/src/stores/useAdminStore.ts`
- `frontend/src/views/AdminView.vue`
- `frontend/src/views/admin/AdminUsersView.vue`
- `frontend/src/views/admin/AdminFeedbacksView.vue`
- `frontend/src/views/admin/AdminStatsView.vue`
- `frontend/src/components/admin/AdminUsersTable.vue`
- `frontend/src/components/admin/AdminUserDetailDrawer.vue`
- `frontend/src/components/admin/AdminFeedbacksTable.vue`
- `frontend/src/components/admin/AdminCreateFeedbackForm.vue`
- `frontend/src/components/admin/AdminStatTile.vue`
