using System.Runtime.CompilerServices;
using System.Text;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
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

    public string Model => _options.Model;

    private const string SystemPrompt =
        "Ты — опытный таролог. Отвечай на русском языке, стиль мистический, но не перегруженный. " +
        "Форматируй ответ в Markdown: используй ## для заголовков позиций расклада, **жирный** для названий карт, " +
        "маркированные списки для ключевых тем. Завершай разделом ## Общий вывод (3–5 предложений).";

    public async Task<InterpretationResult> InterpretAsync(
        Spread spread,
        string question,
        IReadOnlyList<ReadingCard> cards,
        DeckType deckType,
        IReadOnlyDictionary<int, string> variantNotes,
        CancellationToken ct = default)
    {
        var messages = BuildMessages(spread, question, cards, deckType, variantNotes);
        var response = await _chat.CompleteChatAsync(messages, cancellationToken: ct);
        var text = response.Value.Content[0].Text;

        return new InterpretationResult
        {
            Text = text,
            Model = _options.Model,
            GeneratedAt = DateTime.UtcNow
        };
    }

    public async IAsyncEnumerable<string> InterpretStreamAsync(
        Spread spread,
        string question,
        IReadOnlyList<ReadingCard> cards,
        DeckType deckType,
        IReadOnlyDictionary<int, string> variantNotes,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var messages = BuildMessages(spread, question, cards, deckType, variantNotes);
        var stream = _chat.CompleteChatStreamingAsync(messages, cancellationToken: ct);

        await foreach (var update in stream.WithCancellation(ct))
        {
            if (update.ContentUpdate.Count == 0) continue;
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                    yield return part.Text;
            }
        }
    }

    private static List<ChatMessage> BuildMessages(
        Spread spread,
        string question,
        IReadOnlyList<ReadingCard> cards,
        DeckType deckType,
        IReadOnlyDictionary<int, string> variantNotes)
    {
        return new List<ChatMessage>
        {
            new SystemChatMessage(BuildSystemPrompt(deckType)),
            new UserChatMessage(BuildPrompt(spread, question, cards, deckType, variantNotes))
        };
    }

    private static string BuildSystemPrompt(DeckType deckType)
    {
        var deckTone = deckType switch
        {
            DeckType.Thoth =>
                "Работай в традиции колоды Кроули Тота: учитывай каббалистические и астрологические соответствия, " +
                "подчёркивай символизм стихий и планет.",
            DeckType.Marseille =>
                "Работай в традиции колоды Марсель: лаконично, без изобилия образов, опирайся на числовые и геометрические соответствия мастей.",
            DeckType.ViscontiSforza =>
                "Работай в ренессансной традиции Висконти-Сфорца: благородный, исторический, придворный тон.",
            DeckType.ModernWitch =>
                "Работай в современном ведьмовском ключе (Modern Witch Tarot): тёплый, инклюзивный, повседневный язык, " +
                "акцент на практических шагах и эмоциональной честности.",
            _ =>
                "Работай в классической традиции Райдера–Уэйта–Смит: опирайся на каноничные образы и сюжеты."
        };

        return SystemPrompt + " " + deckTone;
    }

    private static string BuildPrompt(
        Spread spread,
        string question,
        IReadOnlyList<ReadingCard> cards,
        DeckType deckType,
        IReadOnlyDictionary<int, string> variantNotes)
    {
        var sb = new StringBuilder();
        sb.Append("Колода: ").AppendLine(deckType.ToString());
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

            if (variantNotes.TryGetValue(rc.CardId, out var note) && !string.IsNullOrWhiteSpace(note))
            {
                sb.Append("  Примечание для колоды ").Append(deckType).Append(": ").AppendLine(note);
            }
        }

        sb.AppendLine();
        sb.AppendLine("Дай развёрнутую интерпретацию расклада применительно к вопросу.");
        return sb.ToString();
    }
}
