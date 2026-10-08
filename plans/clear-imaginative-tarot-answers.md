# Clear, imaginative tarot answers

Give readers a warm, vivid tarot interpretation that answers their actual question. When they ask when something may happen, give an approximate relative interval as a symbolic reading, without promising an event or an exact date.

## Implementation

- Review a bounded sample of recent production readings through read-only access. Keep raw readings outside the repository in a protected temporary directory; record only aggregate findings.
- Update the shared interpreter prompt used by regular and streaming readings: direct opening, connected card imagery, one approximate timing window when requested, and a clear conclusion. Include positive and negative examples; do not force favorable forecasts.
- Replace the conflicting example and blanket prohibition on timing. Preserve privacy filtering, dangerous-question handling, honest AI attribution, and the interface disclosure.
- Preserve the topic of exact-date questions when suggesting an approximate-time rewrite. Retain the acknowledgement flow and restrictions on lottery numbers, guaranteed outcomes, surveillance, and unsafe questions.
- Add an offline synthetic evaluation tool that exports the exact provider messages for fixed cards and safe invented questions. Do not use production questions for external evaluation.
- Leave stored interpretations unchanged; the new voice applies to newly generated readings.
- Preserve an immersive amount of detail after the length discussion: 120–180 words for one card, 250–350 for three, and 450–650 for a large spread. Pass the applicable length and structure in the shared user message as well as the system prompt.

## QA / verification

- Run backend `dotnet test backend/FutureViewer.slnx`, including meaningful timing-rewrite regression tests and existing privacy, safety, ordinary reading, and streaming checks. Docker is available for integration tests.
- Generate regular and streaming synthetic answers with the configured production provider using the exported prompts; review direct answers, approximate timing, negative outcomes, question relevance, and coverage of every card position. Credentials remain on the server; evaluation does not write to the application database.
- Render generated synthetic answers in the actual frontend reading view through an isolated local API fixture and inspect it with the in-app browser. The browser check uses invented data and a local test login; it does not cover a real authenticated production session.
- Run relevant frontend type-check and existing reading-detail/Markdown rendering tests; the UI implementation is unchanged.

## Deploy / push

- Push target: `main` on `origin` (`AlexDragonLore/future-viewer`). Fetch `origin/main`, confirm the commit contains only this task's files, commit the checked changes, then run `git push origin HEAD:main` without force. Verify the remote branch points to the resulting commit.
- The user requested a Git push. Production deployment remains outside this request: `deploy-production.yml` is `workflow_dispatch` only; the push-triggered staging workflow ends with a placeholder and does not update a server.
- No production server/update command will run for this push. A later authorized production release must follow the deployment runbook, update the server checkout with `git fetch origin main` and `git merge --ff-only origin/main`, and use the reviewed prebuilt-image Compose command with `docker-compose.prod.legacy-edge.yml`.
- Post-deploy verification for that later release must include `docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps`, `GET https://alex-taro.ru/health`, and a production browser smoke check. These are not substitutes for verifying the requested Git push.

## Verified result

- Reviewed the latest 80 saved readings. All four timing questions in that sample received no usable time interval. The old prompt both prohibited dates and demonstrated an answer ending without a clear conclusion.
- Full backend suite passed: 10 domain, 201 domain-service, and 242 integration tests (453 total). Timing rewrites preserve the subject, remain accepted on revalidation, and do not bypass existing privacy/safety rules.
- Frontend type-check and 26 existing reading-result, reading-detail, and safe-Markdown tests passed. Repository technical checks passed for all scopes; `git diff --check` passed.
- Initial voice evaluation reviewed 11 synthetic responses from the configured `deepseek-chat` model, including streaming. Fast and slow questions received relative windows; untimed questions stopped receiving unsolicited deadlines. The one-card samples contained 123 and 142 words. All ten cards appeared in the large-spread answer. The evaluation is qualitative and does not guarantee identical phrasing on every generation.
- Inspected positive timing and negative reunion answers rendered in the actual local reading-detail view with the in-app browser. Paragraphs, bold card names, images, and the existing disclosure rendered correctly at the default desktop viewport. No production authenticated browser session or mobile viewport was tested; no frontend implementation changed.
- Production was used only for read-only dialogue review and isolated synthetic provider evaluation. Saved readings, application containers, database, and provider configuration were not changed.
- Before the requested push, set the fuller length targets in both shared prompts and regenerated five synthetic answers. The three-card samples contained 261, 278, and 261 words (the last streamed); the large spread contained 535 words and the single-card sample approximately 120. Full backend tests passed again (453), backend technical checks passed, and the 535-word response rendered correctly in the actual reading-detail view with the in-app browser.
