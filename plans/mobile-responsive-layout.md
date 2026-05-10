# Mobile Responsive Pass, Browser QA, And Server Deploy

## Summary
Make the whole frontend comfortable on mobile down to `320px`, including public pages, auth/profile/history, reading flow, leaderboard, and admin. Verify it carefully in the in-app Browser Use MCP, then deploy the verified branch to the current SSH server using the repo's existing `init.sh` + `docker-compose.prod.yml` production flow.

## Branch And Setup
- Create and switch to `codex/mobile-responsive-layout` before any file changes.
- Copy this plan to `plans/mobile-responsive-layout.md` immediately after Plan Mode approval.
- Keep all code changes on that branch.
- After local verification, push `codex/mobile-responsive-layout` to `origin`.

## Key Changes
- Header/footer:
  - On mobile, keep header compact: burger + logo + minimal right control; move auth, quota, account, and nav links into the burger panel.
  - Make dropdowns viewport-safe.
  - Stop fixed mobile footer from covering bottom content and reading hints.
- Global mobile foundations:
  - Add mobile-safe spacing, wrapping, and typography rules.
  - Reduce large headings/letter-spacing and fix repeated `px-6`, `justify-between`, fixed/min widths, and no-wrap text patterns.
- Reading flow:
  - Update `computeCardWidth`/`computeSlots` so all spreads fit at `320px` without card collisions.
  - Keep long questions, card labels, bottom hints, and animated cards inside visible safe areas.
- Data-heavy screens:
  - Render leaderboard and admin tables as stacked mobile rows/cards at phone widths.
  - Stack admin filters, pagination, drawer fields, and row actions; long emails/UUIDs/statuses must wrap or truncate cleanly.

## Verification Plan
- Run `npm run type-check`.
- Run `npm test`.
- Start Vite with `npm run dev`.
- Use Browser Use MCP against localhost at `320`, `360`, `390`, `430`, `768`, and desktop widths.
- Check home, auth, reading, result, history/detail, profile, achievements, leaderboard, glossary/card detail, feedback, admin users, admin feedbacks, and admin drawer.
- Explicitly verify no horizontal overflow, no header/footer/content overlap, usable forms, working burger menu/dropdowns, readable admin rows, and no browser console errors.

## Server Deploy
- Use the current configured SSH target.
- On the server, use the existing production path `/opt/fv-app/current`.
- Fetch the pushed branch, check out/reset the server worktree to `origin/codex/mobile-responsive-layout`, then run:
  ```bash
  bash init.sh
  ```
- Do not recreate `/opt/fv-app/.env.production`; `init.sh` should reuse the existing production env file.
- After compose finishes, verify:
  ```bash
  docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps
  curl -I https://<configured-domain>/health
  ```
- If SSH target or domain cannot be resolved from the current environment, stop before deployment and ask for the exact SSH host/alias.

## Assumptions
- Minimum supported width is `320px`.
- Scope is the whole frontend, including admin.
- Desktop layouts should remain visually close to current behavior.
- Deployment should publish the verified feature branch directly to the server, not wait for a merge to `main`.
