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
        Spread spread,
        string question,
        IReadOnlyList<ReadingCard> cards,
        CancellationToken ct = default)
    {
        var system = "Ты — опытный таролог. Отвечай на русском языке, стиль мистический, но не перегруженный. " +
                     "Структурируй ответ по позициям расклада, затем дай общий вывод (3-5 предложений).";

        var prompt = BuildPrompt(spread, question, cards);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(system),
            new UserChatMessage(prompt)
        };

        var response = await _chat.CompleteChatAsync(messages, cancellationToken: ct);
        var text = response.Value.Content[0].Text;

        return new InterpretationResult
        {
            Text = text,
            Model = _options.Model,
            GeneratedAt = DateTime.UtcNow
        };
    }

    private static string BuildPrompt(Spread spread, string question, IReadOnlyList<ReadingCard> cards)
    {
        var sb = new StringBuilder();
        sb.Append("Расклад: ").AppendLine(spread.Name);
        if (!string.IsNullOrWhiteSpace(question))
        {
            sb.Append("Вопрос: ").AppendLine(question);
        }
        sb.AppendLine();
        sb.AppendLine("Выпавшие карты:");

        foreach (var rc in cards.OrderBy(c => c.Position))
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
