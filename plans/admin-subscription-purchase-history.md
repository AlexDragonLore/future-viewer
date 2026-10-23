# Admin subscription purchase history

## Implementation
- Add an admin-only, paginated subscription order history backed by existing payment orders, including repeat purchases and anonymized orders after account deletion.
- Default to paid purchases; support all orders, pending, canceled, failed and refunded statuses, email/order search and a user filter. Manual access grants are not purchases.
- Add a responsive Purchases tab and a link from the user drawer to that user's history.

## QA / verification
- Integration tests with PostgreSQL: authorization, filtering, pagination, deleted users and response data boundaries.
- Frontend type check, unit tests and production build.
- Render the page in the in-app browser; exercise desktop and mobile layouts, filters, navigation, errors and pagination using Playwright with synthetic authenticated fixtures. No real payment is needed.

## Deploy / publication
- Target: `origin/main`; work from the latest remote main in an isolated worktree and push the reviewed commit with `git push origin HEAD:main`.
- The user requested a push. Production deployment is a separate manual workflow (`deploy-production.yml`) and is not part of this request.
- If deployment is subsequently requested, the existing workflow performs the server update with Docker Compose using `/opt/fv-app/.env.production`, `docker-compose.prod.yml`, `docker-compose.prod.prebuilt.yml` and project `future-viewer`.
- After that deployment, verify `docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps`, `GET https://alex-taro.ru/health`, and a production browser smoke check.
- For this push, verify the remote main commit and the CI results.

## Verification results
- Backend solution: 464 tests passed (10 domain, 181 services, 273 integration).
- Frontend: 361 unit tests passed; type check and production build passed.
- Browser tests: all 6 purchase history scenarios passed across desktop/mobile, including 320px width, plus 18 existing admin scenarios passed. An initial fixture interception error was corrected before the successful rerun.
- In-app browser: synthetic admin login, rendered purchase list, expanded payment details and refund filtering checked at desktop and 390px mobile width. Real production customer data and payment creation were not used.
