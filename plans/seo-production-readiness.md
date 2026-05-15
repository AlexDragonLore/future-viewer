# SEO And Production Readiness

## Summary
- First implementation target: make `https://alex-taro.ru` look credible in search and social previews, with RU-focused organic SEO for Yandex/Google.
- Current production issues to fix: only one generic `<title>`, no meta description/OG/canonical, `/robots.txt` and `/sitemap.xml` return SPA HTML, `/favicon.ico` is 404.
- Plan file after approval: `plans/seo-production-readiness.md`.

## Key Changes
- Add route-level SEO config for public pages: `/`, `/glossary`, `/legal`, new `/about`, new `/privacy`.
- Add static SEO assets: `robots.txt`, `sitemap.xml`, `site.webmanifest`, favicon/app icons, and a 1200x630 OG image.
- Add post-build SEO generation for Vite output so indexable public routes serve route-specific HTML head tags before JS runs.
- Add client-side SEO updates on navigation for title, description, canonical, robots, Open Graph, Twitter card.
- Mark private/thin/token pages as `noindex`: auth, reset/verify email, feedback tokens, payment success, profile/history/admin/reading/result.
- Replace the jokey "О нас" modal with a serious public `/about` page; add `/privacy` covering email, birth date, questions, feedback, Telegram, payments, and AI usage.
- Add env-backed verification hooks: `VITE_SITE_URL`, optional Google/Yandex verification meta tags, optional analytics IDs without enabling analytics by default.

## Product Seriousness Roadmap
- Immediate: SEO foundation, public trust pages, consistent public brand "Вуаль Грядущего", production search/social preview hygiene.
- Next: static/slugged SEO pages for all 78 cards and key spreads, FAQ content, Search Console/Yandex Webmaster submission, analytics funnel for registration/payment/reading completion.
- Later: Sentry or OpenTelemetry error monitoring, backup/restore drill, rate limiting for auth/AI/payment endpoints, email deliverability checks, cookie/analytics consent if tracking is enabled.

## Public Interfaces
- New public URLs: `/about`, `/privacy`, `/robots.txt`, `/sitemap.xml`, `/site.webmanifest`.
- New frontend env vars: `VITE_SITE_URL`, `VITE_GOOGLE_SITE_VERIFICATION`, `VITE_YANDEX_VERIFICATION`, optionally analytics IDs.
- No required backend API changes in this first implementation.

## QA / Verification
- Run `cd frontend && npm run type-check && npm test && npm run build`.
- Run relevant backend smoke only if routing/deploy config changes touch backend: `dotnet test backend/tests/FutureViewer.DomainServices.Tests/FutureViewer.DomainServices.Tests.csproj`.
- Inspect built `frontend/dist`: verify `robots.txt`, `sitemap.xml`, manifest, favicon, OG image, and per-route generated HTML.
- Use Browser Use/in-app browser for rendered QA on desktop and mobile widths: home, glossary, legal, about, privacy, auth noindex route.
- Verify with `curl -I` and page HTML checks for `/`, `/robots.txt`, `/sitemap.xml`, `/favicon.ico`, `/about`, `/privacy`.

## Deploy
- Branch target: create `codex/seo-production-readiness`, merge to `main` after QA.
- Push branch and open PR; production deploy runs from `main` via existing workflow, or manually with current production process if requested.
- After deploy, verify:
  - `docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps`
  - `GET https://alex-taro.ru/health`
  - production browser smoke check for `/`, `/glossary`, `/about`, `/privacy`
  - `GET https://alex-taro.ru/robots.txt` and `/sitemap.xml` return correct non-HTML content.
- Submit sitemap in Google Search Console and Yandex Webmaster after production verification.

## Assumptions
- RU organic search is the priority.
- First technical step is prerendered/static SEO for public routes, not a full Nuxt/SSR migration.
- Production deployment is included.
- References used: [Google snippets](https://developers.google.com/search/docs/appearance/snippet), [Google title links](https://developers.google.com/search/docs/appearance/title-link), [Google JavaScript rendering guidance](https://developers.google.com/search/docs/crawling-indexing/javascript/dynamic-rendering), [Yandex meta tags](https://yandex.ru/support/webmaster/controlling-robot/meta-robots.html?lang=en), [Yandex sitemaps](https://yandex.ru/support/webmaster/en/controlling-robot/sitemap).
