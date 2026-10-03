# План реагирования на инциденты

Версия: audit draft 2026-08-01. Документ не считается введённым в действие, пока владелец не назначит ответственных, контакты и не проведёт учение. В audit log запрещено сохранять raw questions, AI responses, passwords, tokens, card data или full request bodies.

## 1. Критерии инцидента

Инцидентом считается подтверждённое или обоснованно подозреваемое событие, затрагивающее конфиденциальность, целостность, доступность либо законность обработки, включая:

- раскрытие/несанкционированный доступ к account/profile/question/reading/feedback/payment/log/backup data;
- отправку ПД или специальных категорий непроверенному AI/processor;
- утечку JWT, reset/verification/feedback token, password, DB/provider/SSH key;
- массовый export/history access или admin access вне должностной необходимости;
- успешный XSS, credential stuffing, account/admin takeover;
- webhook forgery/replay, wrong entitlement или payment mismatch;
- смешение alex-taro.ru и janetka.ru networks, volumes, DB, secrets, logs или backups;
- потерю/повреждение БД/backup, ransomware, malicious dependency/image;
- изменение processor endpoint/country/terms без approval;
- нарушение сроков удаления или обнаружение данных после подтверждённого deletion.

## 2. Роли и эскалация

| Роль | Обязанность | Текущий статус |
|---|---|---|
| Incident Commander | Координация, severity, timeline, решения | BLOCKED: лицо не назначено |
| Privacy/PD owner | Scope subjects/data, regulator notifications, DSR impact | BLOCKED: лицо не назначено |
| Technical lead | Containment, evidence, recovery, key rotation | Владелец проекта должен назначить |
| Legal counsel | Applicability/content/timing of notifications | Внешний консультант не подтверждён |
| Communications owner | User/status/support communications | Лицо не назначено |
| Hosting/processor contacts | Emergency suspension, logs, deletion | Контакты/contract references отсутствуют |

До назначения запрещено объявлять process operational. Каналы эскалации должны быть независимы от скомпрометированного production email и храниться во внутреннем защищённом реестре, не в public repo.

## 3. Severity

- **SEV-0:** active mass exfiltration, admin/host compromise, cross-project DB/secret access, encryption/destruction, imminent physical harm from unsafe output.
- **SEV-1:** confirmed exposure of authentication tokens, raw questions/special data, backups, payment integrity; limited but material unauthorized access.
- **SEV-2:** attempted/contained access, limited metadata exposure, processor/config drift without evidence of disclosure.
- **SEV-3:** vulnerability or policy deviation with no observed exploitation.

SEV may only increase until evidence supports documented downgrade.

## 4. Первые действия

1. Record detection time in UTC, reporter, correlation/event IDs and exact systems; do not copy content data into ticket/chat.
2. Activate emergency flags as relevant: `AI`, `privacy export`, payment webhook/payment creation. Feature disable must not delete evidence.
3. Contain exact target: revoke session/key, isolate container/network, block endpoint/host, pause worker/deploy. Avoid host-wide prune or destructive cleanup.
4. Preserve technical evidence read-only: immutable copy/checksum of relevant audit/security logs, container/image digest, config version, DB transaction metadata. Minimize captured PII and document custody/access.
5. Snapshot only when justified and encrypted in approved RF storage; never upload production dump to GitHub/public cloud ad hoc.
6. Notify Incident Commander/Privacy owner/legal/hosting via approved emergency channel.

## 5. Определение состава и субъектов

Build a fact table without raw content:

- first/last observed time and detection time;
- actor/session/admin/provider/correlation identifiers;
- affected tables/categories and approximate record count;
- subject count and jurisdiction/citizenship evidence available;
- whether special, minors, third-party, auth, payment, location/network or communication data were involved;
- whether data were merely accessible, read, exported, changed, deleted or sent externally;
- processors/countries/endpoints involved;
- current containment and residual access;
- confidence, gaps and evidence references.

For affected users use an internal encrypted list accessible only to response roles. Do not put email/question/chat ID in Prometheus labels, Slack/Telegram messages or incident titles.

## 6. Timeline and notifications

The current text of 152-ФЗ provides an initial notification window of 24 hours and results-of-investigation window of 72 hours after the operator identifies an incident involving unauthorized/unlawful transfer/access, when the statutory duty applies. Legal owner must determine applicability; engineering must prepare the facts early enough and must not wait for full certainty to start escalation.

### Initial notification pack

- incident time/detection time;
- assumed cause;
- affected categories and approximate subjects;
- assumed harm;
- containment measures;
- designated contact person;
- explicit unknowns and investigation plan.

### Investigation-results pack

- confirmed timeline/root cause/attack vector;
- exact categories/subjects/operations;
- responsible actor if determined;
- technical/organizational measures taken;
- user/provider actions and residual risk;
- evidence references and postmortem owner.

User notification, processor notification, law enforcement/payment partner contact and public status update are decided by the Incident Commander with Privacy/legal input. Communications must be plain, factual, non-speculative and must not disclose other users' data or security details that increase risk.

## 7. Key/session rotation

Order depends on attack path:

1. disable compromised integration/session;
2. revoke all affected refresh/access sessions and increment security version;
3. rotate exposed provider/JWT/DB/SMTP/payment/SSH keys using dual-key transition where possible;
4. update approved secret store and deploy through controlled pipeline;
5. invalidate verification/reset/feedback tokens whose logs/storage were exposed;
6. verify old keys fail and search for continued use;
7. rotate janetka credentials too only if evidence shows shared exposure; coordinate separately and never assume authority from a Future Viewer incident.

## 8. Recovery and backups

- Restore into isolated environment first; verify integrity/schema/malware and apply deletion ledger before service activation.
- Compare DB audit/event counts, payment ledger and provider state.
- Do not activate subscriptions from redirects or unverifiable webhooks during recovery.
- Confirm AI/export/payment emergency flags remain at intended state.
- Perform staged health/readiness and browser checks; monitor auth failures, history access and exports.
- Record RPO/RTO and any missing/deleted data without placing content in the incident log.

## 9. Required technical detection

The target system must produce content-free alerts for:

- repeated failed login by IP-HMAC/email-HMAC/device window;
- admin login/MFA/reset/role change and access to sensitive resources;
- mass export or abnormal privacy export frequency;
- mass reading/history enumeration/download/delete;
- unusual webhook replay/signature failures/amount mismatch;
- processor endpoint/registry/config/hash change;
- AI gateway PII/special-category block spike (category/count only);
- cross-project network/volume/mount drift;
- backup failure/restore-test expiry;
- deployment without required compliance/security checks.

Audit event fields: UTC time, event type, random correlation ID, actor opaque ID/session ID, target type/opaque ID, outcome/reason code, source IP HMAC or truncated prefix only when justified, user-agent HMAC only when justified, config/document version. Forbidden: raw question, AI output, feedback text, email, chat ID, token, password, provider response body, full URL/query/body.

## 10. Postmortem

Within the internally approved period after containment:

- blameless timeline and root cause;
- why preventive/detective controls failed;
- affected data/users/processors and notification decisions;
- corrective actions with owner/due date/evidence;
- tests/monitoring/runbook/processor/register updates;
- deletion of temporary evidence copies when legal/security need ends;
- tabletop or replay validation;
- risk-register status update without claiming compliance beyond evidence.

## 11. Emergency switches

Фактические server-side switches и соответствующие production env-переменные:

- `AI:Enabled` / `AI_ENABLED=false` — запрет внешнего AI;
- `Privacy:ExportEnabled` / `PRIVACY_EXPORT_ENABLED=false` — аварийный запрет новых экспортов;
- `Payment:Enabled` / `PAYMENT_ENABLED=false` — запрет создания новых оплат;
- `Payment:WebhookEnabled` / `PAYMENT_WEBHOOK_ENABLED=false` — запрет обработки payment webhook.

AI, создание платежей и payment webhook выключены в шаблоне production по умолчанию. Privacy export включён по умолчанию как право пользователя, но может быть временно отключён при подтверждённом incident; такое отключение подлежит регистрации, обоснованию и скорейшему восстановлению.

Disabling a feature returns a stable user-safe status, does not log request content and does not erase evidence. Сейчас switches меняются через protected GitHub environment либо ограниченный SSH-доступ к production env; встроенных MFA/step-up для этой операции и внешнего append-only audit sink в проекте нет. До их внедрения каждое изменение должен выполнить назначенный оператор по двухстороннему approval с записью времени, actor, причины и ticket ID во внешнем защищённом журнале без пользовательского контента.

При ручном изменении production env то же аварийное значение нужно установить в protected GitHub environment variable до следующего deployment. Иначе workflow восстановит declarative значение из GitHub environment.

## 12. Exercise checklist

- Simulate leaked AI key and unintended raw-question transfer.
- Simulate stolen admin JWT and mass history access.
- Simulate invalid/replayed payment webhook.
- Simulate shared-host Docker/volume mistake affecting janetka.
- Restore an encrypted backup, apply deletion ledger and prove deleted user remains deleted.
- Draft 24-hour and 72-hour packs from technical events only.

Until contacts, providers, backup process and exercises are evidenced, incident readiness remains an open P0/P1 risk.
