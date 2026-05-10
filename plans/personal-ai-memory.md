# Персональное знакомство, память AI и валидация вопросов

## Summary
Добавить персонализацию текущего потока раскладов без отдельного чат-раздела: авторизованный пользователь перед первым раскладом заполняет имя, фамилию и дату рождения. Перед генерацией AI-ответа backend валидирует вопрос через отдельный AI-валидатор. Если вопрос принят, в промпт интерпретации добавляются текущая дата, профиль пользователя и до 20 сохранённых фактов памяти. После ответа backend отдельным AI-шагом извлекает полезные факты и сохраняет их для будущих раскладов.

## Key Changes
- Добавить персонализацию пользователя:
  - поля `FirstName`, `LastName`, `BirthDate` в `User`;
  - новую сущность `UserMemoryRule` с `Id`, `UserId`, `Text`, `CreatedAt`, `UpdatedAt`;
  - EF-конфигурацию, `DbSet`, миграцию и репозиторий для профиля/памяти.
- Добавить защищённые API:
  - `GET /api/profile/personalization`;
  - `PUT /api/profile/personalization`;
  - `DELETE /api/profile/personalization/memory/{id}`;
  - `DELETE /api/profile/personalization/memory`.
- Расширить `CreateReadingRequest` полями `clientDate` (`yyyy-MM-dd`) и `clientTimeZone`; backend использует UTC-дату как fallback.
- Перед созданием расклада backend проверяет заполненность профиля; если данных знакомства нет, возвращает `409` с `error: "profile_required"`.
- Добавить `IAIQuestionValidator`:
  - запускается в `ReadingService` до вытягивания карт и до интерпретации;
  - использует предоставленный prompt валидатора, но формат ответа упрощается до JSON без `is_valid`;
  - строгий JSON: `status`, `reason`, `suggested_question`;
  - `accepted` пропускает расклад дальше;
  - `needs_rewrite` возвращает `422` с `error: "question_needs_rewrite"` и `suggestedQuestion`;
  - `rejected` возвращает `422` с `error: "question_rejected"` и причиной.
- Перед вызовом интерпретатора `ReadingService` загружает профиль и до 20 правил памяти, формирует `UserPromptContext` и передаёт его в `InterpretationService`/`IAIInterpreter`.
- `OpenAIInterpreter` добавляет в system prompt:
  - сегодняшний день и часовой пояс;
  - имя, фамилию, дату рождения;
  - список сохранённых правил памяти;
  - инструкцию использовать память только когда она релевантна вопросу.
- Добавить `IAIMemoryExtractor`:
  - запускается после успешной нестриминговой интерпретации и после завершения стриминга;
  - сохраняет только устойчивые факты, полезные для будущих ответов;
  - не сохраняет предсказания из расклада;
  - дедуплицирует похожие пункты и держит максимум 20 правил.

## Validator JSON
Финальный формат ответа валидатора:

```json
{
  "status": "accepted | needs_rewrite | rejected",
  "reason": "короткое объяснение на русском",
  "suggested_question": "улучшенная версия вопроса или null"
}
```

Правила:
- `accepted`: вопрос нормальный, `suggested_question=null`;
- `needs_rewrite`: вопрос подходит по смыслу, но требует переформулировки;
- `rejected`: вопрос бессмысленный, опасный или неподходящий.

## Frontend
- На `HomeView` для авторизованного пользователя без профиля показывать блок знакомства внутри формы расклада: имя, фамилия, дата рождения.
- Кнопка “Начать расклад” сначала сохраняет профиль, затем запускает существующий переход в `ReadingView`.
- `readingApi.create` и `createStream` передают `clientDate` и `clientTimeZone`.
- Если backend возвращает `question_needs_rewrite`, показывать причину и предложенную формулировку с кнопкой подставить её в поле вопроса.
- Если backend возвращает `question_rejected`, показывать мягкое объяснение без запуска расклада.
- В `ProfileView` добавить блок “Знакомство и память”: редактирование имени/фамилии/даты рождения, список AI-памяти до 20 пунктов, удаление отдельных пунктов и очистка всей памяти.

## Test Plan
- Backend unit tests:
  - вопрос `accepted` продолжает создание расклада;
  - `needs_rewrite` и `rejected` останавливают расклад до вытягивания карт;
  - `ReadingService` передаёт профиль, память и дату в `IAIInterpreter`;
  - memory service сохраняет не больше 20 правил.
- Integration tests:
  - `GET/PUT /api/profile/personalization`;
  - удаление одного и всех memory rules;
  - `/api/readings` и `/api/readings/stream` требуют профиль;
  - невалидный вопрос возвращает структурированную ошибку без созданного расклада.
- Frontend tests:
  - `HomeView` показывает поля знакомства только когда профиль не заполнен;
  - suggested question можно подставить в textarea;
  - `ProfileView` отображает и удаляет memory rules.
- Manual verification:
  - `dotnet test backend/FutureViewer.slnx`;
  - `cd frontend && npm test && npm run type-check`.

## Assumptions
- Персонализация доступна только авторизованным пользователям.
- Валидатор вызывается перед каждым раскладом, потому что каждый вопрос может быть неподходящим.
- Память сохраняется автоматически после ответа и не требует подтверждения пользователя.
