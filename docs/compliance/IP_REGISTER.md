# Реестр объектов интеллектуальных прав

Дата повторного аудита: 25 сентября 2026 года. Статус `BLOCKED` означает, что коммерческое использование нельзя считать подтверждённым только по репозиторию. Такой объект должен быть отключён production gate, заменён либо подтверждён архивируемым доказательством и ручным review.

## Реестр

| Объект | Файлы/объём | Автор/правообладатель | Источник | Лицензия | Коммерческое использование | Атрибуция | Подтверждающий файл/договор | Статус/действие |
|---|---|---|---|---|---|---|---|---|
| Rider–Waite–Smith card artwork | 78 JPG в `frontend/public/cards/rws/` и/или card asset dirs | Pamela Colman Smith; original deck published 1909/1910; Arthur Edward Waite associated text/concept | `frontend/scripts/download-cards.sh` использует Wikimedia `Special:FilePath`; per-file source revision не сохранён | Wikimedia pages для sample files помечают public domain, но jurisdiction/per-file proof не архивирован | Вероятно разрешено для original public-domain artwork, но производный scan/source terms требуют проверки | Commons attribution/source желательно сохранить, даже когда не обязательна | Отсутствуют per-file URL, revision, license snapshot и SHA-256 manifest | BLOCKED до manifest + legal verification; не смешивать с modern derivative decks |
| Modern Witch deck references | `frontend/src/data/decks.ts:12-63` и copy | Фактический правообладатель НЕИЗВЕСТЕН | Название/описание в repository content | НЕИЗВЕСТНО | НЕ ПОДТВЕРЖДЕНО | НЕИЗВЕСТНО | Отсутствует | BLOCKED: не показывать images/claims implying licensed deck until contract |
| Thoth deck references | `frontend/src/data/decks.ts:12-63` и copy | Фактический правообладатель НЕИЗВЕСТЕН | Repository content | НЕИЗВЕСТНО | НЕ ПОДТВЕРЖДЕНО | НЕИЗВЕСТНО | Отсутствует | BLOCKED pending legal verification; descriptive factual reference needs separate review from artwork use |
| Tarot card meanings/SEO texts | `frontend/src/data/tarotSeoCatalog.js:1-592`; backend `TarotDeckSeed` meanings/keywords/advice | Git history shows repository contributor `aleksandr`; exact author/source and assignment not documented | Repository commits including `56330e0`, `17e6cf9`, earlier seeds | Root `LICENSE` attempts project licensing but author/operator spelling conflicts and third-party boundaries are unclear | НЕ ПОДТВЕРЖДЕНО | Project attribution per final license | Git commits only; no author declaration/source notes | BLOCKED until owner declares original authorship/assignment or texts are replaced with newly commissioned originals |
| Spread/deck descriptions | `frontend/src/data/decks.ts`, `frontend/src/data/spreads.ts`, related views | Repository contributor; exact provenance unverified | Git history | Project license unclear for content | НЕ ПОДТВЕРЖДЕНО | Per final project terms | Git commits only | BLOCKED pending author declaration or replacement |
| AI prompts/system wording | `backend/src/FutureViewer.Infrastructure/AI/*.cs` | Repository contributor(s) | Source repository | Root project license | Project use appears intended, but third-party copied wording not documented | Not normally needed | Git history | REVIEW; rewrite during privacy remediation and record commit |
| AI-generated interpretations | Runtime user-specific outputs | AI provider output + user input | Runtime | Provider contract/account terms and output rights not archived | НЕ ПОДТВЕРЖДЕНО; also privacy-sensitive | Service disclosure required, not necessarily attribution | Provider contract reference missing | BLOCKED external AI until provider approval; do not claim exclusivity/originality |
| Logo/favicon/icons/OG | `frontend/public/favicon.svg`, `frontend/public/icons/**`, `frontend/public/og/**` | Git author `aleksandr`; designer/source not documented | Repository commits including `17e6cf9`, `56330e0`; exact source asset chain unclear | Root project license | НЕ ПОДТВЕРЖДЕНО | Per final license | Source design files/author declaration absent | BLOCKED pending author declaration or replacement with documented project-native assets |
| Lucide icons | `lucide-vue-next` dependency and runtime mappings | Lucide contributors | npm package | ISC (must verify installed package license/version) | Generally permits commercial use | Preserve license notice | `frontend/package-lock.json`; package LICENSE not vendored in public asset register | CONDITIONAL: automate dependency license report and retain notices |
| Google Fonts: Inter, Cinzel | Старые remote links удалены из `frontend/index.html`; CSS использует system fallbacks | Respective font authors | В текущем bundle файлы Google Fonts не поставляются | Не применимо к текущей поставке | Внешнее использование прекращено | Не требуется для нераспространяемых файлов | `config/assets.json` фиксирует `shipped=false` | REMOVED; при будущем self-hosting нужны exact WOFF2, OFL и hashes |
| System fonts | Georgia, Times New Roman, Arial, system-ui fallbacks | OS/browser vendors | User device | OS licenses | Rendering use via CSS generally covered by device license | None | N/A | ALLOWED as fallback; no redistribution |
| Procedural sound | `frontend/src/composables/useAudio.ts` | Repository contributor; generated Web Audio tones | Source code | Project license | Intended project use | Per source license | Git history | REVIEW author/license; no third-party audio asset found |
| Photographs | No photograph asset category identified beyond card scans/SEO graphics | — | — | — | — | — | — | NOT PRESENT based on audit |
| Music files | None found; audio is procedural | — | — | — | — | — | — | NOT PRESENT |
| Video files | None found | — | — | — | — | — | — | NOT PRESENT |
| GSAP | npm dependency | GreenSock | npm/upstream | Upstream license/standard terms must be checked for exact version/use | НЕ ПОДТВЕРЖДЕНО by repo audit | Per license | package lock only | REVIEW in automated license scan |
| marked, axios, Vue, Pinia, Vue Router, Tailwind, Vite, DOM/testing libraries | npm dependencies | Upstream contributors | npm registry | Multiple OSS licenses | Conditional on individual licenses/notices | Preserve required notices | `frontend/package-lock.json` | REVIEW via generated SBOM/license report; high-risk/unknown licenses block build |
| .NET/NuGet libraries: Npgsql/EF Core/OpenAI SDK/MailKit/BCrypt/etc. | `backend/**/*.csproj`, resolved packages | Upstream contributors/vendors | NuGet | Multiple OSS licenses/provider terms | Conditional | Preserve required notices | `.csproj`, NuGet lock absent | REVIEW; enable lock file/SBOM/license scan |
| Templates/build configs | Dockerfiles, workflows, Caddy/Nginx, tests | Repository contributor(s), possibly adapted snippets | Git history | Root project license | Intended project use; upstream templates not documented | Per source if copied | Git history only | REVIEW/owner declaration |

## Проверенные внешние карточные источники — ограниченное доказательство

Эти страницы подтверждают только конкретные sample files и не заменяют manifest всех 78 файлов:

- `RWS Tarot 00 Fool.jpg`: https://commons.wikimedia.org/wiki/File:RWS_Tarot_00_Fool.jpg
- `Wands01.jpg`: https://commons.wikimedia.org/wiki/File:Wands01.jpg

Wikimedia records identify Pamela Colman Smith and public-domain reasoning for those files. До архивации точного source URL/revision/license text/hash каждой карты нельзя переносить вывод на весь набор автоматически.

## Требуемый asset manifest

Для каждого shipped asset:

```text
path
sha256
asset_type
title
author
source_reference
license_spdx_or_public_domain_basis
commercial_use
attribution_text
evidence_path
approved_by
approved_at
```

Проверка `scripts/lib/compliance-evidence.mjs` раскрывает `path_glob` каждой одобренной поставляемой группы (включая `{a,b}` и разделённые `;` шаблоны), читает JSON по `sha256_manifest` и сверяет точный набор файлов и SHA-256. Формат JSON: `{ "schema_version": 1, "asset_id": "идентификатор группы", "files": [...] }`; каждая запись `files` содержит перечисленные выше обязательные сведения (`source_reference` — URL, revision или ссылка на заявление автора). `sha256` — 64 шестнадцатеричных символа без префикса. Поля `asset_type`, `title`, `attribution_text` рекомендуются для описания, но не проверяются автоматически.

Несуществующие/пустые локальные evidence-файлы, дубликаты, пропущенные/посторонние материалы, изменение содержимого, будущая дата одобрения и выход через symlink за пределы репозитория блокируют проверку. Для новой версии материала требуется повторное одобрение. Проверка целостности не подтверждает юридическую достаточность договора или подлинность заявления автора: содержимое evidence проверяет владелец/юрист. Неподтверждённые группы по-прежнему блокируют выпуск; новые проверки не проставляют approval.

Команда проверки негативных сценариев: `node --test scripts/tests/*.test.mjs` (Node.js 22.17+).

## Правила замены/удаления

- Не копировать значения карт с других сайтов без разрешения.
- Не использовать modern deck artwork/branding только из-за визуального сходства с public-domain RWS.
- Не считать изображение public domain исключительно из-за отсутствия watermark.
- Для заказных материалов хранить договор с явной передачей/лицензией на commercial online use, derivative works и territory/term.
- Для AI-generated assets хранить prompt/output date/provider/model, human review, provider terms snapshot и проверку на third-party marks/characters; не обещать авторское право там, где оно юридически неопределённо.
- Если доказательство нельзя получить, объект отключается feature/build flag либо заменяется документированным original/public-domain asset.

## Ручное решение

До подтверждения IP register production gate должен оставаться закрыт. Особо проверить различие фамилий автора в `LICENSE` и public operator defaults: это не позволяет автоматически установить цепочку прав.
