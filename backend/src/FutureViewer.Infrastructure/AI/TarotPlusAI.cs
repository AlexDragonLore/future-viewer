using System.ClientModel;
using System.Text.Json;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace FutureViewer.Infrastructure.AI;

public sealed class TarotPlusAI : ITarotPlusAI
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ChatClient _chat;
    private readonly ILogger<TarotPlusAI> _logger;
    private readonly TimeSpan _fastTimeout;
    private readonly TimeSpan _followUpTimeout;
    private readonly TimeSpan _reportTimeout;

    public TarotPlusAI(IOptions<TarotPlusAIOptions> options, IOptions<DeepSeekOptions> deepSeekOptions, ILogger<TarotPlusAI> logger)
    {
        var tarotOptions = options.Value;
        var deepSeek = deepSeekOptions.Value;
        _logger = logger;

        var apiKey = string.IsNullOrWhiteSpace(tarotOptions.ApiKey) ? deepSeek.ApiKey : tarotOptions.ApiKey;
        var baseUrl = string.IsNullOrWhiteSpace(tarotOptions.BaseUrl) ? deepSeek.BaseUrl : tarotOptions.BaseUrl;
        Model = string.IsNullOrWhiteSpace(tarotOptions.Model) ? "deepseek-v4-pro" : tarotOptions.Model;
        _fastTimeout = TimeSpan.FromSeconds(Math.Clamp(tarotOptions.FastRequestTimeoutSeconds, 5, 45));
        _followUpTimeout = TimeSpan.FromSeconds(Math.Clamp(tarotOptions.FollowUpRequestTimeoutSeconds, 10, 90));
        _reportTimeout = TimeSpan.FromSeconds(Math.Clamp(tarotOptions.ReportRequestTimeoutSeconds, 30, 180));
        var timeoutSeconds = Math.Clamp(
            Math.Max(tarotOptions.RequestTimeoutSeconds, (int)_reportTimeout.TotalSeconds),
            30,
            180);

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("TarotPlusAI:ApiKey or DeepSeek:ApiKey is not configured");

        var clientOptions = new OpenAIClientOptions
        {
            NetworkTimeout = TimeSpan.FromSeconds(timeoutSeconds)
        };
        if (!string.IsNullOrWhiteSpace(baseUrl))
            clientOptions.Endpoint = new Uri(baseUrl, UriKind.Absolute);

        var client = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions);
        _chat = client.GetChatClient(Model);
    }

    public string Model { get; }

    public async Task<TarotPlusRouteResult> RouteAsync(
        TarotPlusInterviewContext context,
        CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("""
Ты — безопасный маршрутизатор для платной Tarot+ сессии.
Верни строго JSON без markdown:
{
  "route": "relationship|career|money|decision|self_identity|family|general_life|resource_state|safety_sensitive",
  "preview_text": "мягкий preview на русском, 3-5 предложений",
  "safety_flags": ["..."]
}

Нельзя давать медицинские диагнозы, юридические решения, инвестиционные советы, фатальные предсказания, запугивание,
утверждения что другой человек точно думает или чувствует, манипулятивные инструкции или инструкции по самоповреждению.
Если тема чувствительная, route="safety_sensitive" и preview_text должен мягко предложить опору и обращение к профильному специалисту.
"""),
            new UserChatMessage(JsonSerializer.Serialize(context, JsonOptions))
        };

        var json = await CompleteJsonAsync(messages, _fastTimeout, "route", ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new TarotPlusRouteResult
        {
            Route = ParseRoute(ReadString(root, "route")),
            PreviewText = ReadString(root, "preview_text") ?? FallbackPreview(context),
            SafetyFlags = ReadStringArray(root, "safety_flags")
        };
    }

    public async Task<TarotPlusQuestionResult> NextQuestionAsync(
        TarotPlusInterviewContext context,
        CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("""
Ты ведёшь бережное Tarot+ интервью перед большим отчётом.
Верни строго JSON без markdown:
{
  "ready_for_report": true|false,
  "question": "следующий короткий вопрос на русском или null",
  "safety_flags": ["..."]
}

Задавай только один вопрос за раз. Не проси персональные документы, контакты, диагнозы, юридические детали или финансовые суммы.
После 5 содержательных ответов можно вернуть ready_for_report=true, если контекста достаточно. После 9 ответов всегда ready_for_report=true.
"""),
            new UserChatMessage(JsonSerializer.Serialize(context, JsonOptions))
        };

        var json = await CompleteJsonAsync(messages, _fastTimeout, "next_question", ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new TarotPlusQuestionResult
        {
            ReadyForReport = ReadBool(root, "ready_for_report"),
            Question = ReadNullableString(root, "question"),
            SafetyFlags = ReadStringArray(root, "safety_flags")
        };
    }

    public async Task<TarotPlusReportResult> GenerateReportAsync(
        TarotPlusReportContext context,
        CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("""
Ты пишешь глубокий Tarot+ отчёт на русском языке по уже вытянутым backend-ом картам.
Не придумывай карты и не меняй позиции. Используй только переданные расклады и ответы.

Структура markdown:
# Жизненный компас
## Главная тема
## Что показывают карты
## Слепые зоны и ресурсы
## Рекомендации на 7 дней
## Рекомендации на 30 дней
## Рекомендации на 90 дней
## Вопросы для самопроверки

Запрещено: медицинские диагнозы, юридические решения, инвестиционные советы, фатальные предсказания, запугивание,
утверждения что другой человек точно думает или чувствует, манипуляции и инструкции по самоповреждению.
Формулируй как рефлексию и практические ориентиры, а не как гарантированный прогноз.
"""),
            new UserChatMessage(JsonSerializer.Serialize(context, JsonOptions))
        };

        try
        {
            var response = await RunWithTimeout(
                "report",
                _reportTimeout,
                innerCt => _chat.CompleteChatAsync(messages, cancellationToken: innerCt),
                ct);
            return new TarotPlusReportResult
            {
                ReportMarkdown = ReadContent(response.Value)
            };
        }
        catch (ClientResultException ex)
        {
            _logger.LogError(ex, "Tarot+ report generation failed");
            throw;
        }
    }

    public async Task<TarotPlusFollowUpResult> AskFollowUpAsync(
        TarotPlusFollowUpContext context,
        CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("""
Ты отвечаешь на уточняющий вопрос по готовому Tarot+ отчёту.
Ответь markdown на русском, 3-6 коротких абзацев. Не тяни новые карты и не придумывай новые позиции.
Сохраняй безопасные рамки: не давай диагнозы, юридические решения, инвестиционные инструкции, фатальные предсказания,
манипулятивные советы или утверждения о чужих мыслях как о факте.
"""),
            new UserChatMessage(JsonSerializer.Serialize(context, JsonOptions))
        };

        try
        {
            var response = await RunWithTimeout(
                "follow_up",
                _followUpTimeout,
                innerCt => _chat.CompleteChatAsync(messages, cancellationToken: innerCt),
                ct);
            return new TarotPlusFollowUpResult
            {
                AnswerMarkdown = ReadContent(response.Value)
            };
        }
        catch (ClientResultException ex)
        {
            _logger.LogError(ex, "Tarot+ follow-up failed");
            throw;
        }
    }

    private async Task<string> CompleteJsonAsync(
        IEnumerable<ChatMessage> messages,
        TimeSpan timeout,
        string operationName,
        CancellationToken ct)
    {
        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        try
        {
            var response = await RunWithTimeout(
                operationName,
                timeout,
                innerCt => _chat.CompleteChatAsync(messages, options, innerCt),
                ct);
            return ReadContent(response.Value);
        }
        catch (ClientResultException ex)
        {
            _logger.LogError(ex, "Tarot+ JSON AI call failed");
            throw;
        }
    }

    private async Task<T> RunWithTimeout<T>(
        string operationName,
        TimeSpan timeout,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        try
        {
            return await operation(timeoutCts.Token);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                ex,
                "Tarot+ AI operation {OperationName} timed out after {TimeoutSeconds}s",
                operationName,
                timeout.TotalSeconds);
            throw new TimeoutException($"Tarot+ AI operation {operationName} timed out", ex);
        }
    }

    private static string ReadContent(ChatCompletion completion)
    {
        var text = completion.Content.Count > 0 ? completion.Content[0].Text : null;
        if (string.IsNullOrWhiteSpace(text))
            throw new JsonException("AI returned empty content");
        return text;
    }

    private static string FallbackPreview(TarotPlusInterviewContext context) =>
        $"Запрос «{context.CoreRequest}» выглядит как тема для глубокого разбора. " +
        "После оплаты я задам несколько уточняющих вопросов, затем карты помогут собрать практичный жизненный компас.";

    private static TarotPlusRoute ParseRoute(string? route) =>
        route switch
        {
            "relationship" => TarotPlusRoute.Relationship,
            "career" => TarotPlusRoute.Career,
            "money" => TarotPlusRoute.Money,
            "decision" => TarotPlusRoute.Decision,
            "self_identity" => TarotPlusRoute.SelfIdentity,
            "family" => TarotPlusRoute.Family,
            "general_life" => TarotPlusRoute.GeneralLife,
            "resource_state" => TarotPlusRoute.ResourceState,
            "safety_sensitive" => TarotPlusRoute.SafetySensitive,
            _ => TarotPlusRoute.GeneralLife
        };

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? ReadNullableString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static bool ReadBool(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value)
        && (value.ValueKind == JsonValueKind.True
            || value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed) && parsed);

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        return value.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString()?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Take(10)
            .ToList();
    }
}
