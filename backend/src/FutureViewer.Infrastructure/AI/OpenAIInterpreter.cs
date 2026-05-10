using System.Runtime.CompilerServices;
using System.Text;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.Domain.ValueObjects;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;
using OpenAI.Chat;

namespace FutureViewer.Infrastructure.AI;

public sealed class OpenAIInterpreter : IAIInterpreter
{
    private readonly AIChatClientFactory _chatClientFactory;
    private readonly ChatClient _chat;

    public OpenAIInterpreter(AIChatClientFactory chatClientFactory)
    {
        _chatClientFactory = chatClientFactory;
        _chat = chatClientFactory.CreateChatClient();
    }

    public string Model => _chatClientFactory.Model;

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
        UserPromptContext promptContext,
        CancellationToken ct = default)
    {
        var messages = BuildMessages(spread, question, cards, deckType, variantNotes, promptContext);
        var response = await _chat.CompleteChatAsync(messages, cancellationToken: ct);
        var text = response.Value.Content[0].Text;

        return new InterpretationResult
        {
            Text = text,
            Model = Model,
            GeneratedAt = DateTime.UtcNow
        };
    }

    public async IAsyncEnumerable<string> InterpretStreamAsync(
        Spread spread,
        string question,
        IReadOnlyList<ReadingCard> cards,
        DeckType deckType,
        IReadOnlyDictionary<int, string> variantNotes,
        UserPromptContext promptContext,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var messages = BuildMessages(spread, question, cards, deckType, variantNotes, promptContext);
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
        IReadOnlyDictionary<int, string> variantNotes,
        UserPromptContext promptContext)
    {
        return new List<ChatMessage>
        {
            new SystemChatMessage(BuildSystemPrompt(deckType, promptContext)),
            new UserChatMessage(BuildPrompt(spread, question, cards, deckType, variantNotes))
        };
    }

    private static string BuildSystemPrompt(DeckType deckType, UserPromptContext promptContext)
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

        var sb = new StringBuilder();
        sb.Append(SystemPrompt).Append(' ').AppendLine(deckTone);
        sb.Append("Сегодняшний день: ").Append(promptContext.Today.ToString("yyyy-MM-dd"));
        if (!string.IsNullOrWhiteSpace(promptContext.ClientTimeZone))
            sb.Append(" (часовой пояс пользователя: ").Append(promptContext.ClientTimeZone).Append(')');
        sb.AppendLine(".");
        sb.Append("Профиль пользователя: ")
            .Append(promptContext.FirstName).Append(' ')
            .Append(promptContext.LastName)
            .Append(", дата рождения: ")
            .Append(promptContext.BirthDate.ToString("yyyy-MM-dd"))
            .AppendLine(".");

        if (promptContext.MemoryRules.Count > 0)
        {
            sb.AppendLine("Сохранённая память о пользователе, которую можно учитывать только когда она релевантна вопросу:");
            foreach (var rule in promptContext.MemoryRules.Take(20))
            {
                sb.Append("- ").AppendLine(rule);
            }
        }

        sb.AppendLine("Не упоминай память явно и не делай выводы о пользователе как факты, если это не помогает ответу.");
        return sb.ToString();
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
