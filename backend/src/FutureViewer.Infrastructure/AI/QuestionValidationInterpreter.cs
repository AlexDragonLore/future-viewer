using System.ClientModel;
using System.Text.Json;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.Services;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace FutureViewer.Infrastructure.AI;

public sealed class QuestionValidationInterpreter : IAIQuestionValidator
{
    private readonly ChatClient _chat;
    private readonly ILogger<QuestionValidationInterpreter> _logger;

    private const string SystemPrompt = """
Ты — валидатор вопросов для расклада Таро.

Твоя задача: проверить пользовательский вопрос и вернуть СТРОГО JSON без markdown.

Таро-вопрос считается хорошим, если он:
- понятен и не является набором случайных слов;
- связан с реальной жизненной ситуацией пользователя;
- не слишком общий, но и не требует невозможной точности;
- лучше сформулирован открыто: "что", "как", "на что обратить внимание", "какие возможности/риски";
- помогает пользователю получить совет, понимание ситуации или направление для размышления;
- фокусируется на действиях, чувствах, выборе или состоянии самого пользователя;
- не требует вторжения в мысли/чувства другого человека как факт;
- не требует гарантированного предсказания будущего;
- не просит медицинский диагноз, юридическое решение, финансовую гарантию или опасный совет.

Не будь слишком строгим:
- можно пропускать вопросы про любовь, работу, деньги, отношения, выбор, будущее;
- можно пропускать простые вопросы вроде "Что меня ждёт в отношениях?";
- можно пропускать вопросы да/нет, если их можно нормально трактовать через Таро, но лучше предложить улучшенную формулировку;
- не придирайся к грамматике, опечаткам и разговорному стилю.

Не пропускай:
- бессмысленный текст: "ываыва", "что по кайфу", "скажи всё";
- слишком расплывчатые вопросы без темы: "что будет?", "ну как там?";
- вопросы с требованием точного факта: "когда он напишет точную дату?", "какой номер выиграет?";
- вопросы про контроль другого человека: "как заставить его вернуться?";
- вопросы про слежку/манипуляции: "изменяет ли он мне, скажи точно";
- медицинские, юридические, финансовые гарантии: "есть ли у меня рак?", "стоит ли вложить все деньги?";
- вредные или опасные запросы.

Формат ответа строго такой:

{
  "status": "accepted" | "needs_rewrite" | "rejected",
  "reason": "короткое объяснение на русском",
  "suggested_question": "улучшенная версия вопроса или null"
}

Правила:
1. Если вопрос нормальный — status="accepted", suggested_question=null.
2. Если вопрос в целом подходит, но его лучше переформулировать — status="needs_rewrite", suggested_question=улучшенная версия.
3. Если вопрос бессмысленный, опасный или неподходящий — status="rejected", suggested_question=null или безопасная мягкая альтернатива.
4. Не добавляй текст вне JSON.
5. Не делай эзотерических утверждений как фактов.
6. Улучшенная формулировка должна быть мягкой, открытой и полезной.
""";

    public QuestionValidationInterpreter(
        AIChatClientFactory chatClientFactory,
        ILogger<QuestionValidationInterpreter> logger)
    {
        _chat = chatClientFactory.CreateChatClient();
        _logger = logger;
    }

    public async Task<QuestionValidationResult> ValidateAsync(string question, CancellationToken ct = default)
    {
        var localDecision = QuestionValidationHeuristics.TryValidate(question);
        if (localDecision is not null)
            return localDecision;

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(SystemPrompt),
            new UserChatMessage($"Пользовательский вопрос:\n{question}")
        };
        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        try
        {
            var response = await _chat.CompleteChatAsync(messages, options, ct);
            return ParseResult(response.Value.Content[0].Text);
        }
        catch (ClientResultException ex)
        {
            _logger.LogWarning(ex, "AI question validation failed; using local fallback");
            return Fallback(question);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse question validation response; using local fallback");
            return Fallback(question);
        }
    }

    private static QuestionValidationResult ParseResult(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var statusText = root.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.String
            ? s.GetString()
            : null;
        var status = statusText switch
        {
            "needs_rewrite" => QuestionValidationStatus.NeedsRewrite,
            "rejected" => QuestionValidationStatus.Rejected,
            _ => QuestionValidationStatus.Accepted
        };
        var reason = root.TryGetProperty("reason", out var r) && r.ValueKind == JsonValueKind.String
            ? r.GetString() ?? "Вопрос принят."
            : "Вопрос принят.";
        var suggested = root.TryGetProperty("suggested_question", out var q) && q.ValueKind == JsonValueKind.String
            ? q.GetString()
            : null;

        return new QuestionValidationResult
        {
            Status = status,
            Reason = reason,
            SuggestedQuestion = string.IsNullOrWhiteSpace(suggested) ? null : suggested
        };
    }

    private static QuestionValidationResult Accepted(string reason) => new()
    {
        Status = QuestionValidationStatus.Accepted,
        Reason = reason,
        SuggestedQuestion = null
    };

    private static QuestionValidationResult Fallback(string question)
    {
        return QuestionValidationHeuristics.TryValidate(question)
            ?? Accepted("Вопрос принят локальной проверкой.");
    }
}
