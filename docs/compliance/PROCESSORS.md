# Реестр внешних получателей и инфраструктурных обработчиков

Актуально после технической remediation на 1 августа 2026 года. Машиночитаемый источник истины — `config/processors.json`; этот документ объясняет его фактическое состояние. Значение `false (blocked)` означает, что код и release gate запрещают интеграцию, а не что поставщик проверен.

## Правило допуска

Передача разрешается только когда одновременно заполнены и доказаны: `provider_name`, `purpose`, `legal_entity`, `country`, exact encrypted `endpoint`, `data_categories`, `retention`, `uses_data_for_training`, `cross_border_transfer`, `contract_reference`, `enabled=true` и `manual_approved=true`. Для AI дополнительно обязательно `uses_data_for_training=false`; пользовательский текст проходит `AiPrivacyGateway`. Неизвестное поле означает запрет.

Runtime сверяет точное имя, тип и endpoint записи. Для web/API допустим только HTTPS; для SMTP — `smtps://host:465` либо `smtp+starttls://host:587|25`. Feature flag или secret сами по себе не разрешают передачу.

## Фактический реестр

| provider_name | purpose | legal_entity | country | endpoint | data_categories | retention | uses_data_for_training | cross_border_transfer | contract_reference | enabled |
|---|---|---|---|---|---|---|---|---|---|---|
| Production Docker host | App/API/PostgreSQL/container logs | НЕИЗВЕСТНО | НЕИЗВЕСТНО | Runtime host отсутствует в repo | Все данные основной БД, сетевые метаданные, технические logs | НЕИЗВЕСТНО | unknown | unknown | НЕИЗВЕСТНО | false (release blocked) |
| Primary PostgreSQL | Первичная запись, систематизация, хранение и извлечение | НЕИЗВЕСТНО | НЕИЗВЕСТНО | `postgresql://postgres:5432/future_viewer` внутри isolated network | Account/profile/consents/readings/feedback/payment/audit/DSR | proposed config, не утверждён | false | unknown | НЕИЗВЕСТНО | false (release blocked) |
| Backup storage | Encrypted backup/restore | НЕИЗВЕСТНО | НЕИЗВЕСТНО | НЕИЗВЕСТНО | Потенциально полный dump БД | НЕИЗВЕСТНО | unknown | unknown | НЕИЗВЕСТНО | false |
| OpenAI API | AI-интерпретация после gateway | НЕИЗВЕСТНО для договора проекта | НЕИЗВЕСТНО для аккаунта/маршрута | `https://api.openai.com/v1` | Только minimized pseudonymous prompt, карты, random request ID | НЕИЗВЕСТНО для проекта | unknown | unknown | НЕИЗВЕСТНО | false |
| DeepSeek API | Опциональная AI-интерпретация после gateway | НЕИЗВЕСТНО | НЕИЗВЕСТНО | `https://api.deepseek.com` | Только minimized pseudonymous prompt, карты, random request ID | НЕИЗВЕСТНО | unknown | unknown | НЕИЗВЕСТНО | false |
| Custom OpenAI-compatible endpoint | Непроверенный AI transport | НЕИЗВЕСТНО | НЕИЗВЕСТНО | Не задан; произвольные providers кодом не поддерживаются | Передача запрещена | НЕИЗВЕСТНО | unknown | unknown | НЕИЗВЕСТНО | false |
| SMTP provider | Verification/reset и транзакционные письма | НЕИЗВЕСТНО | НЕИЗВЕСТНО | Точный SMTPS/STARTTLS host должен прийти из env | Email, one-time link, message metadata; body не логируется | НЕИЗВЕСТНО | unknown | unknown | НЕИЗВЕСТНО | false |
| Support mailbox provider | Поддержка, претензии, DSR | НЕИЗВЕСТНО | НЕИЗВЕСТНО | НЕИЗВЕСТНО | Email и содержание обращения пользователя | НЕИЗВЕСТНО | unknown | unknown | НЕИЗВЕСТНО | false; support address не публикуется |
| YooKassa | Hosted checkout/status verification | НЕИЗВЕСТНО для merchant contract | НЕИЗВЕСТНО | `https://api.yookassa.ru/v3/` | Opaque order ID, amount, currency, payment status; без email/internal user ID/PAN/CVV | НЕИЗВЕСТНО | unknown | unknown | НЕИЗВЕСТНО | false |
| YooMoney | Hosted redirect/HMAC webhook | НЕИЗВЕСТНО для receiver contract | НЕИЗВЕСТНО | `https://yoomoney.ru/quickpay/confirm` | Opaque order ID, amount, currency, status; без email/internal user ID/PAN/CVV | НЕИЗВЕСТНО | unknown | unknown | НЕИЗВЕСТНО | false |
| Google Fonts | Remote font delivery | Не применимо после remediation | — | Внешние URLs удалены из shipped HTML | Передача IP/UA/referrer отсутствует в текущем bundle | — | — | false в текущем коде | — | false; removed |
| GitHub and GitHub Actions | Source, CI/security/deploy orchestration | НЕИЗВЕСТНО для аккаунта | НЕИЗВЕСТНО | `https://github.com` | Source/CI metadata/deployment secrets; customer data prohibited | НЕИЗВЕСТНО | unknown | likely/unknown | НЕИЗВЕСТНО | false в registry до owner review; фактически repo/CI используется |
| DNS and domain registrar | DNS/domain registration | НЕИЗВЕСТНО | НЕИЗВЕСТНО | НЕИЗВЕСТНО | Domain registration и DNS/network metadata | НЕИЗВЕСТНО | unknown | unknown | НЕИЗВЕСТНО | unknown / release blocked |
| TLS certificate authority | Public TLS certificate | Фактический issuer виден только live | НЕИЗВЕСТНО | НЕИЗВЕСТНО | Domain/certificate/server metadata | НЕИЗВЕСТНО | false | unknown | НЕИЗВЕСТНО | unknown / release blocked |
| NPD receipt channel | Чек «Мой налог»/уполномоченный канал | НЕИЗВЕСТНО | НЕИЗВЕСТНО | НЕИЗВЕСТНО | Payment/receipt data и контакт покупателя, когда требуется | По налоговым требованиям; не утверждено | unknown | unknown | НЕИЗВЕСТНО | false |
| Yandex Metrica | Необязательная аналитика | НЕ ПРОВЕРЕНО | НЕ ПРОВЕРЕНО | `https://mc.yandex.ru` | Сейчас ничего: loader отсутствует | НЕИЗВЕСТНО | unknown | unknown | Отсутствует | false |
| Google Analytics | Необязательная аналитика | НЕ ПРОВЕРЕНО | НЕ ПРОВЕРЕНО | GTM/GA endpoints в registry, loader отсутствует | Сейчас ничего | НЕИЗВЕСТНО | unknown | unknown | Отсутствует | false |
| Sentry/PostHog/OpenTelemetry/Prometheus | Monitoring/analytics | Не применимо к текущему коду | — | SDK/config/transport не найдены | Сейчас ничего | — | — | — | — | false/not present |

## Что технически изменено

- AI, SMTP и payment clients проверяют processor registry непосредственно перед созданием клиента/передачей; совпадение имени и endpoint точное, substring approval запрещён.
- `AI_ENABLED`, `PAYMENT_ENABLED` и `PAYMENT_WEBHOOK_ENABLED` выключены по умолчанию.
- AI получает только результат локальной минимизации; email, ФИО, birth year/date, Telegram ID, IP, user-agent и внутренний user UUID в payload не добавляются.
- Payment provider получает только random public order ID и параметры заказа.
- Analytics loaders и remote Google Fonts отсутствуют в production bundle; cookie consent не является разрешением добавить непроверенного provider.

## Нерешённое

По репозиторию нельзя установить реального production AI/SMTP/payment provider, физическую страну host/PostgreSQL/backups, стороны договоров, sub-processors, retention или режим обучения. `/opt/fv-app/.env.production`, provider dashboards и договоры не проверялись. Поэтому внешние интеграции и production release остаются заблокированы.

Общие публичные правила OpenAI API указывают на no-training-by-default и возможное хранение abuse-monitoring logs до 30 дней, однако это не доказывает настройки или договор конкретного аккаунта: [OpenAI data controls](https://platform.openai.com/docs/models/default-usage-policies-by-endpoint), [OpenAI API data usage](https://help.openai.com/en/articles/5722486-api-data-usage-policies).
