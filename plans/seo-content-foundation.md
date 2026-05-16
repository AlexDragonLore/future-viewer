# SEO Content Foundation

## Summary
- Текущая индексация настроена корректно: публичные страницы индексируются, приватные и тонкие страницы закрыты `noindex`, `robots.txt` и `sitemap.xml` работают.
- Главная проблема охвата: в индексе сейчас только 5 полезных URL. Для роста органики нужно расширить публичную SEO-поверхность.
- Первый фокус на 2 недели: серьезный русскоязычный эзотерический контент вокруг карт Таро, раскладов и колод.

## Key Changes
- Добавить индексируемые публичные страницы 78 карт Таро по стабильным латинским slug URL: `/tarot/cards/shut`, `/tarot/cards/mag`, `/tarot/cards/verhovnaya-zhritsa`, `/tarot/cards/tuz-kubkov`.
- Создать статический frontend SEO-каталог карт: название, аркан/масть, прямое и перевернутое значение, ключевые темы, краткое описание, meta title/description, связанные карты.
- Оставить текущий `/glossary/:id` закрытым от индекса или заменить его внутренними ссылками на новые slug-страницы, чтобы в поиске жили только человекочитаемые URL.
- Добавить индексируемые страницы раскладов: `/tarot/spreads/karta-dnya`, `/tarot/spreads/tri-karty`, `/tarot/spreads/keltskiy-krest`.
- Добавить индексируемые страницы колод: `/tarot/decks/rider-waite-smith`, `/tarot/decks/thoth`, `/tarot/decks/marseille`, `/tarot/decks/visconti-sforza`, `/tarot/decks/modern-witch`.
- Добавить `/faq` с практичными вопросами: как формулировать вопрос, как читать перевернутые карты, чем расклады отличаются, можно ли гадать на отношения/работу/решение.
- Расширить генерацию SEO после Vite build: route-specific HTML для новых URL, canonical, `index, follow`, sitemap, Open Graph/Twitter preview.
- Добавить внутреннюю перелинковку: главная и глоссарий ведут на card/spread/deck hubs; страницы карт ведут на похожие карты, расклады и CTA; FAQ ведет на основные продуктовые страницы.
- Добавить structured data: `BreadcrumbList`, `WebPage`/`Article`, `FAQPage`.
- Не открывать к индексации `/leaderboard`, `/auth`, `/profile`, `/history`, `/reading`, `/result`, `/feedback`, `/admin`, платежные и token-страницы.
- Артефакты подтверждения Google/Yandex оставить.

## QA / Verification
- Run `cd frontend && npm run type-check`.
- Run `cd frontend && npm test`.
- Run `cd frontend && npm run build`.
- Inspect `frontend/dist`: generated card/spread/deck/FAQ HTML, sitemap entries, robots, and noindex pages.
- Browser Use / in-app browser QA on desktop and mobile: `/`, `/glossary`, `/tarot/cards/shut`, `/tarot/spreads/tri-karty`, `/tarot/decks/rider-waite-smith`, `/faq`, `/leaderboard`.
- Production curl checks after deploy: `/robots.txt`, `/sitemap.xml`, `/tarot/cards/shut`, `/tarot/spreads/tri-karty`, `/faq`, and `/leaderboard` remains `noindex`.

## Deploy
- Branch target: `codex/seo-content-foundation`.
- Push branch, open PR, merge to `main` after QA.
- Deploy through the existing production process.
- Verify production:
  - `docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps`
  - `GET https://alex-taro.ru/health`
  - production browser smoke check for home, glossary, card page, spread page, deck page, FAQ.
- Submit updated sitemap in Google Search Console and Yandex Webmaster.
- Request indexing for priority URLs first: `/`, `/glossary`, `/faq`, first 10 card pages, all spread pages.

## Assumptions
- RU organic search is the main channel.
- Tone: serious эзотерический, без шуточной подачи.
- No paid ads or social campaign in this first 2-week foundation.
- No backend API changes required for v1.
- Slugs use Latin transliteration for stable sharing and cleaner canonical URLs.
- References: [Google SEO Starter Guide](https://developers.google.com/search/docs/fundamentals/seo-starter-guide), [Google structured data](https://developers.google.com/search/docs/appearance/structured-data/intro-structured-data), [Google sitemaps](https://developers.google.com/search/docs/crawling-indexing/sitemaps/overview), [Yandex sitemap guidance](https://yandex.com/support/webmaster/en/robot-workings/sitemap).
