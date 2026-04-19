using System.ClientModel;
using System.Text.Json;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace FutureViewer.Infrastructure.AI;

public sealed class FeedbackScoringInterpreter : IFeedbackScorer
{
    private readonly OpenAIOptions _options;
    private readonly ChatClient _chat;
    private readonly ILogger<FeedbackScoringInterpreter> _logger;

    private const string SystemPrompt =
        "Ты — строгий но справедливый аналитик. Получаешь: вопрос пользователя, AI-интерпретацию таро-расклада, " +
        "и самоотчёт пользователя о том, как он следовал рекомендациям интерпретации. " +
        "Твоя задача — оценить: (1) насколько искренне написан самоотчёт и (2) насколько пользователь реально следовал " +
        "рекомендациям интерпретации. " +
        "Признаки неискренности: пустой/шаблонный/скопированный текст, общие фразы без конкретики, противоречия, " +
        "откровенно сгенерированный AI текст, слишком идеальное совпадение с рекомендациями. " +
        "Если ответ неискренний — isSincere=false, score=1. " +
        "Если искренний — score от 1 (не следовал) до 10 (следовал полностью и осознанно). " +
        "Отвечай строго валидным JSON вида: {\"score\": число 1-10, \"reason\": \"на русском, 1-3 предложения\", \"isSincere\": true|false}. " +
        "Никаких markdown, никакого текста вне JSON.";

    public FeedbackScoringInterpreter(
        IOptions<OpenAIOptions> options,
        ILogger<FeedbackScoringInterpreter> logger)
    {
        _options = options.Value;
        _logger = logger;
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("OpenAI:ApiKey is not configured");

        _chat = new OpenAIClient(_options.ApiKey).GetChatClient(_options.Model);
    }

    public async Task<FeedbackScoringResult> ScoreAsync(
        string question,
        string interpretation,
        string selfReport,
        CancellationToken ct = default)
    {
        var userMessage =
            $"Вопрос пользователя:\n{question}\n\n" +
            $"Интерпретация расклада:\n{interpretation}\n\n" +
            $"Самоотчёт пользователя:\n{selfReport}";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(SystemPrompt),
            new UserChatMessage(userMessage)
        };

        var chatOptions = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        try
        {
            var response = await _chat.CompleteChatAsync(messages, chatOptions, ct);
            var text = response.Value.Content[0].Text;
            return ParseResult(text);
        }
        catch (ClientResultException ex)
        {
            _logger.LogError(ex, "OpenAI scoring call failed");
            return new FeedbackScoringResult
            {
                Score = 1,
                Reason = "Не удалось получить оценку из-за технической ошибки.",
                IsSincere = false
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse OpenAI scoring response");
            return new FeedbackScoringResult
            {
                Score = 1,
                Reason = "Ответ AI не удалось распарсить.",
                IsSincere = false
            };
        }
    }

    private static FeedbackScoringResult ParseResult(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var score = 1;
        if (root.TryGetProperty("score", out var s) && s.ValueKind == JsonValueKind.Number)
        {
            if (s.TryGetInt32(out var i)) score = i;
            else if (s.TryGetDouble(out var d)) score = (int)Math.Round(d);
        }

        var reason = root.TryGetProperty("reason", out var r) && r.ValueKind == JsonValueKind.String
            ? r.GetString() ?? string.Empty
            : string.Empty;

        var isSincere = ParseIsSincere(root);

        return new FeedbackScoringResult
        {
            Score = Math.Clamp(score, 1, 10),
            Reason = reason,
            IsSincere = isSincere
        };
    }

    internal static bool ParseIsSincere(JsonElement root)
    {
        if (!root.TryGetProperty("isSincere", out var sincere))
            return false;

        return sincere.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(sincere.GetString(), out var b) && b,
            JsonValueKind.Number => sincere.TryGetInt32(out var n) && n != 0,
            _ => false
        };
    }
}
