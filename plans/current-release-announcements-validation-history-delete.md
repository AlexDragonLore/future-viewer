# Current Release: Validation Warnings, History Deletion, Announcements

## Summary
- After Plan Mode exit, save this plan as `plans/current-release-announcements-validation-history-delete.md`.
- Change question validation from a hard stop into a subscription-aware warning flow:
  - subscribers can continue after explicit confirmation;
  - non-subscribers see "На такие запросы можно ответить только с подпиской" plus a rewrite recommendation.
- Add soft delete for reading history: hidden from the user's history/detail/feedback surfaces, retained internally.
- Add authenticated announcements with a header bell; unread messages disappear after the user marks them viewed.
- Seed one current-release announcement, not April.

## Key Changes
- Backend API/contracts:
  - Extend `CreateReadingRequest` with `QuestionWarningAcknowledged: bool = false`.
  - Change `POST /api/readings/validate-question` to return `200 OK` for all validator statuses:
    `status`, `reason`, `suggestedQuestion`, `message`, `canContinue`, `requiresSubscription`.
  - Add guarded server enforcement on reading creation/streaming:
    subscribers need acknowledgement for non-accepted questions; free users get `402 question_requires_subscription`.
  - Add `DELETE /api/readings/{id}` for idempotent owner-only soft delete.
  - Add `GET /api/announcements/unread` and `POST /api/announcements/{id}/read`.

- Backend data:
  - Add `readings.deleted_from_history_at`.
  - Filter deleted readings from user history, user reading detail, feedback links, and pending feedback notifications.
  - Keep deleted readings in quotas, achievements, admin stats, and admin user detail because this is soft delete.
  - Add `announcements` and `announcement_reads` tables with unique `announcement.code` and `(user_id, announcement_id)`.

- Frontend:
  - `HomeView` shows:
    - subscriber confirmation modal for non-accepted questions with "Продолжить" and suggested rewrite;
    - free-user subscription warning plus suggested rewrite and payment banner.
  - `ReadingView` passes `questionWarningAcknowledged` from `sessionStorage` into stream creation.
  - `HistoryView` adds a trash icon button with confirmation text saying the reading is deleted completely, then removes the card from the list.
  - `SiteHeader` adds a lucide bell button for authenticated users, unread badge, announcement dropdown, and "Понятно" action that marks the message read.

- Current announcement copy:
  - Title: `Что нового`
  - Body: `В текущем обновлении спорные вопросы больше не обрывают расклад для подписчиков: Вуаль покажет предупреждение и предложит более удачную формулировку. В истории теперь можно полностью удалить расклад из личного архива. А новые обновления будут приходить в колокольчик и исчезать после просмотра.`

## QA / Verification
- Backend:
  - `dotnet test backend/tests/FutureViewer.DomainServices.Tests/FutureViewer.DomainServices.Tests.csproj`
  - `dotnet test backend/tests/FutureViewer.Integration.Tests/FutureViewer.Integration.Tests.csproj` with Docker running.
  - Cover subscriber warning acknowledgement, free-user subscription block, soft-deleted history/detail hiding, feedback suppression, and announcement read-state persistence.

- Frontend:
  - `cd frontend && npm run type-check`
  - `cd frontend && npm test`
  - `cd frontend && npm run build`
  - Add/update tests for `HomeView`, `ReadingView`, `HistoryView`, and `SiteHeader`.

- Browser QA with in-app browser:
  - Run the local app stack.
  - Verify desktop and mobile widths:
    - subscriber invalid question shows warning, suggestion, and can continue;
    - free invalid question shows subscription-only message;
    - history delete removes the card and survives reload;
    - bell shows current announcement, "Понятно" removes it, reload keeps it hidden.
  - If authenticated browser state cannot be reproduced in Browser Use, cover the exact state with component tests and note that limitation.

## Assumptions
- Announcements are only for authenticated users.
- "After viewing, message is deleted" means deleted from that user's unread list via a read marker, not physically removed from the global announcement table.
- "Удаляется полностью" is user-facing wording for history removal; backend still keeps the row for soft-delete, quotas, achievements, admin/debug, and audit-compatible behavior.
