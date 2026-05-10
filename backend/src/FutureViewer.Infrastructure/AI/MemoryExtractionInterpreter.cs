using System.ClientModel;
using System.Text.Json;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace FutureViewer.Infrastructure.AI;

public sealed class MemoryExtractionInterpreter : IAIMemoryExtractor
{
    private readonly ChatClient _chat;
    private readonly ILogger<MemoryExtractionInterpreter> _logger;

    private const string SystemPrompt =
        "Ты извлекаешь краткую долгосрочную память для будущих таро-интерпретаций. " +
        "Верни строго JSON вида {\"rules\":[\"...\"]}. " +
        "Сохраняй только устойчивые факты о пользователе, его целях, важных контекстах, предпочтениях и жизненных обстоятельствах, " +
        "если они явно следуют из вопроса пользователя. Не сохраняй предсказания, советы из расклада, догадки, медицинские/юридические/финансовые выводы, " +
        "не сохраняй имя, фамилию или дату рождения пользователя — они уже хранятся в профиле отдельно, " +
        "и не дублируй уже существующую память. Каждое правило должно быть на русском, короткое, нейтральное, до 160 символов. " +
        "Если сохранять нечего, верни {\"rules\":[]}.";

    public MemoryExtractionInterpreter(
        AIChatClientFactory chatClientFactory,
        ILogger<MemoryExtractionInterpreter> logger)
    {
        _chat = chatClientFactory.CreateChatClient();
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> ExtractAsync(MemoryExtractionContext context, CancellationToken ct = default)
    {
        var userMessage =
            "Текущая память:\n" +
            string.Join("\n", context.PromptContext.MemoryRules.Select(r => "- " + r)) +
            $"\n\nВопрос пользователя:\n{context.Question}\n\n" +
            $"AI-интерпретация:\n{context.Interpretation}";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(SystemPrompt),
            new UserChatMessage(userMessage)
        };
        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        try
        {
            var response = await _chat.CompleteChatAsync(messages, options, ct);
            return ParseRules(response.Value.Content[0].Text);
        }
        catch (ClientResultException ex)
        {
            _logger.LogWarning(ex, "AI memory extraction failed");
            return Array.Empty<string>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI memory extraction response");
            return Array.Empty<string>();
        }
    }

    private static IReadOnlyList<string> ParseRules(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("rules", out var rules) || rules.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        return rules.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString()?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Length > 500 ? x[..500] : x)
            .Take(20)
            .ToList();
    }
}
