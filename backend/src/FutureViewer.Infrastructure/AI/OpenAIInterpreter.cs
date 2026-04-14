using System.Text;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.ValueObjects;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace FutureViewer.Infrastructure.AI;

public sealed class OpenAIInterpreter : IAIInterpreter
{
    private readonly OpenAIOptions _options;
    private readonly ChatClient _chat;

    public OpenAIInterpreter(IOptions<OpenAIOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("OpenAI:ApiKey is not configured");

        _chat = new OpenAIClient(_options.ApiKey).GetChatClient(_options.Model);
    }

    public async Task<InterpretationResult> InterpretAsync(
        Reading reading,
        Spread spread,
        CancellationToken ct = default)
    {
        var system = "Ты — опытный таролог. Отвечай на русском языке, стиль мистический, но не перегруженный. " +
                     "Структурируй ответ по позициям расклада, затем дай общий вывод (3-5 предложений).";

        var prompt = BuildPrompt(reading, spread);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(system),
            new UserChatMessage(prompt)
        };

        var response = await _chat.CompleteChatAsync(messages, cancellationToken: ct);
        var text = response.Value.Content[0].Text;

        return new InterpretationResult(text, _options.Model, DateTime.UtcNow);
    }

    private static string BuildPrompt(Reading reading, Spread spread)
    {
        var sb = new StringBuilder();
        sb.Append("Расклад: ").AppendLine(spread.Name);
        if (!string.IsNullOrWhiteSpace(reading.Question))
        {
            sb.Append("Вопрос: ").AppendLine(reading.Question);
        }
        sb.AppendLine();
        sb.AppendLine("Выпавшие карты:");

        foreach (var rc in reading.Cards.OrderBy(c => c.Position))
        {
            var position = spread.Positions[rc.Position];
            var orientation = rc.IsReversed ? "перевёрнутая" : "прямая";
            var meaning = rc.IsReversed ? rc.Card.DescriptionReversed : rc.Card.DescriptionUpright;
            sb.Append("- ").Append(position.Name).Append(" (").Append(position.Meaning).Append("): ")
              .Append(rc.Card.Name).Append(" [").Append(orientation).Append("] — ").AppendLine(meaning);
        }

        sb.AppendLine();
        sb.AppendLine("Дай развёрнутую интерпретацию расклада применительно к вопросу.");
        return sb.ToString();
    }
}
