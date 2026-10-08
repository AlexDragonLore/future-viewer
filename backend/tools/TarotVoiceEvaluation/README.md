# Tarot voice evaluation

Exports synthetic questions and fixed cards through the exact private `OpenAIInterpreter.BuildMessages` method. Every question passes through `AiPrivacyGateway` first. The tool does not instantiate an AI client, read credentials, query a database, or contact a provider.

From the repository root:

```sh
dotnet run --project backend/tools/TarotVoiceEvaluation -- /tmp/tarot-voice-cases.json
```

Without a destination argument, the JSON is written to standard output. The exported array contains `id`, `question`, `messages` (provider-ready `role` and `content`), and `stream`. A fixed synthetic technical request ID keeps exports comparable. Capture an export before changing the prompt and another afterward for a baseline comparison; the cards and questions remain the same.

## Qualitative review

Generate answers with the same provider, model, and settings for both exports. Review the answers themselves; mocked provider responses or string checks of the prompt cannot establish voice quality.

| Case | What to review |
| --- | --- |
| `timing-positive` | Gives a useful approximate range early, linked to the fast-moving cards, without guaranteeing an event or an exact date. |
| `timing-positive-single` | Answers the timing question with a useful approximate range from one Eight of Wands; the position named "Advice" must not turn the answer into an evasive suggestion. |
| `timing-slow` | Gives a slower approximate range consistent with the pause in the cards instead of reusing the fast case's timing. |
| `timing-negative-immediate` | Answers the requested immediate timing clearly and preserves the negative forecast; does not promise a message just to provide a date. |
| `relationship-mixed` | Gives a readable main answer to the yes/no question, explains the mixed cards, and avoids an invented psychological diagnosis or biography. |
| `reunion-negative` | Allows a clear disappointing answer rather than turning every spread into reassurance or a promised reunion. |
| `reunion-negative-single` | Gives a clear symbolic negative answer from one upright Ten of Swords; does not refuse because there is only one card or invent a guaranteed reunion to soften it. |
| `clear-choice` | Chooses a direction and explains it through the cards instead of leaving the question unanswered. |
| `existing-period` | Interprets the week requested in the question without replacing it with an unrelated future time range. |
| `large-spread` | Covers all ten positions, connects them to the project, and remains readable with a clear opening answer. |
| `timing-positive-stream` | Uses the same timing scenario with streaming enabled; review the opening answer and coherence of the completed response. |

Across cases, review specificity, natural Russian, warmth, appropriate imagery, repetition, and whether the answer feels complete. Review the intended volume too: approximately 120–180 words for one card, 250–350 for three, and 450–650 for the large spread. Keep the distinction between a symbolic forecast and an established fact. The tool deliberately contains no real conversations or personal data.
