using System.Runtime.CompilerServices;
using System.Text;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.Domain.ValueObjects;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;
using OpenAI.Chat;

namespace FutureViewer.Infrastructure.AI;

public sealed class OpenAIInterpreter : IAIInterpreter
{
    private readonly AIChatClientFactory _chatClientFactory;
    private readonly ChatClient _chat;
    private readonly IAiPrivacyGateway _privacyGateway;

    public OpenAIInterpreter(
        AIChatClientFactory chatClientFactory,
        IAiPrivacyGateway privacyGateway)
    {
        _chatClientFactory = chatClientFactory;
        _chat = chatClientFactory.CreateChatClient();
        _privacyGateway = privacyGateway;
    }

    public string Model => _chatClientFactory.Model;

    private const string SystemPrompt =
        "Ты создаёшь развлекательную и информационную интерпретацию карт Таро на русском языке. " +
        "Описывай только возможный взгляд, темы для размышления и варианты — не выдавай интерпретацию за факт, диагноз или достоверное предсказание. " +
        "Не давай медицинских, психологических, юридических или финансовых указаний; не рекомендуй лекарства, инвестиции, кредиты или юридически значимые действия. " +
        "Не обещай точный, гарантированный результат и не утверждай, что AI знает будущее. " +
        "Если контекст выглядит срочным или опасным, предложи обратиться к профильному специалисту или экстренной службе, не развивая гадание. " +
        "Стиль может быть мягко мистическим, но не перегруженным. " +
        "Форматируй ответ в Markdown: используй ## для заголовков позиций расклада, **жирный** для названий карт, " +
        "маркированные списки для ключевых тем. Завершай разделом ## Возможный взгляд (3–5 предложений) и кратким напоминанием, что важные решения нельзя основывать только на этом результате.";

    public async Task<InterpretationResult> InterpretAsync(
        Spread spread,
        string question,
        IReadOnlyList<ReadingCard> cards,
        DeckType deckType,
        IReadOnlyDictionary<int, string> variantNotes,
        UserPromptContext promptContext,
        CancellationToken ct = default)
    {
        _ = promptContext; // Identity/profile/timezone/memory must not leave the service.
        var privacy = Prepare(question);
        var messages = BuildMessages(
            spread,
            privacy.SafeText,
            cards,
            deckType,
            variantNotes,
            privacy.RequestId);
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
        _ = promptContext; // Identity/profile/timezone/memory must not leave the service.
        var privacy = Prepare(question);
        var messages = BuildMessages(
            spread,
            privacy.SafeText,
            cards,
            deckType,
            variantNotes,
            privacy.RequestId);
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
        Guid requestId)
    {
        return new List<ChatMessage>
        {
            new SystemChatMessage(BuildSystemPrompt(deckType)),
            new UserChatMessage(BuildPrompt(spread, question, cards, deckType, variantNotes, requestId))
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

        var sb = new StringBuilder();
        sb.Append(SystemPrompt).Append(' ').AppendLine(deckTone);
        sb.AppendLine("У тебя нет профиля или идентификатора пользователя. Не пытайся установить личность и не запрашивай персональные данные.");
        return sb.ToString();
    }

    private static string BuildPrompt(
        Spread spread,
        string question,
        IReadOnlyList<ReadingCard> cards,
        DeckType deckType,
        IReadOnlyDictionary<int, string> variantNotes,
        Guid requestId)
    {
        var sb = new StringBuilder();
        sb.Append("Технический request ID: ").AppendLine(requestId.ToString("N"));
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

    private AiPrivacyDecision Prepare(string question)
    {
        var decision = _privacyGateway.Prepare(question, AiPrivacyOperation.TarotInterpretation);
        if (!decision.CanSendExternally)
            throw new AiPrivacyBlockedException(
                decision.ReasonCode,
                decision.UserMessage,
                decision.SafeResponse);
        return decision;
    }
}
