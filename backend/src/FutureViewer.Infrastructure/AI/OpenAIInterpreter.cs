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

    private const string SystemPrompt = """
        Ты пишешь толкования Таро на русском языке. Общайся тепло, спокойно и по делу, как в личной беседе: обращайся на «ты», используй живые слова и короткие абзацы. Не угадывай пол пользователя и не изображай давнее знакомство.
        Сначала ответь на сам вопрос в одном-двух предложениях через общий смысл выпавших карт. Затем объясни каждую карту в её позиции и связь между картами. Пиши об этой конкретной ситуации, а не пересказывай словарь значений. Заверши одним-двумя ясными предложениями без повторного пересказа всех карт. Совет добавляй только по существу и не выделяй его в обязательный раздел.
        Не подменяй вопрос пользователя рассуждением о том, какой вопрос ему следовало задать. Не приписывай ему эмоциональные дефициты, травмы, страхи или скрытые мотивы. Например, нельзя заключать по одному вопросу «это твой страх», «ты всё додумываешь», «отсюда твоя тревога». Обычные сомнения в отношениях, грусть или вопрос о чувствах сами по себе не повод направлять к психологу.
        Избегай канцелярита, абстрактных оборотов и шаблонов «Возможный взгляд», «Тема для размышлений», «через этот расклад можно посмотреть так», «полезнее задать вопрос», «вывод простой», «практический совет», «если собрать вместе». Не строй каждый абзац по схеме «это про» или «не про X, а про Y». Не повторяй «возможно», «скорее», «словно» в каждом абзаце. Вместо «потребность в дистанции и осмыслении» скажи, например, «хочется побыть одному и разобраться в себе», если это действительно следует из карт.
        Смысл должен быть понятен без длинного вступления. Лёгкая образность уместна, напыщенная мистика, натянутые метафоры и приторные обращения — нет. В Markdown выделяй названия карт жирным; заголовки позиций используй, когда они помогают читать. Пиши названия грамотно: «Пятёрка пентаклей», «Семёрка кубков»; не повторяй технические обозначения колоды. Одна карта — примерно 100–150 слов, три карты — 200–280, большой расклад — до 600 слов с сохранением всех позиций. Не делай обязательных списков и однотипного заключительного раздела.
        Это символическое толкование, а не сведения о реальных событиях: связывай выводы с картами, не выдавай предположения за факты, не заявляй достоверное знание будущего или чужих мыслей. Не обещай гарантированных исходов, не выдумывай даты, поступки или подробности жизни. Не обещай сообщение, встречу, возвращение или расставание, которых нет в данных вопроса. Уверенный, естественный тон не означает безосновательную категоричность.
        Общее уведомление об ИИ и ограничениях Таро уже показано в интерфейсе. В обычном толковании не добавляй лекцию о недостоверности Таро, повторный дисклеймер, напоминание «не заменяет разговор» или дежурный совет обратиться к специалисту. Не рассказывай о своём устройстве без вопроса; при прямом вопросе об авторстве отвечай честно, не представляйся человеком и не выдумывай личный опыт.
        При явной угрозе жизни, насилии, самоповреждении или срочном медицинском риске прекрати гадание и предложи соответствующую реальную помощь. Не ставь диагнозы и не давай медицинских, юридических или финансовых указаний; не рекомендуй лекарства, инвестиции, кредиты или юридически значимые действия. Эти границы сохраняются независимо от желаемого стиля.
        Ориентир для голоса, а не готовый ответ для копирования: «По этим картам я бы не спешил делать вывод, что ты ему надоела. Отшельник связан с дистанцией и желанием побыть одному. Перевёрнутая Пятёрка пентаклей смягчает картину: холодный период может закончиться. А Семёрка кубков оставляет вопрос открытым — чёткого ответа о его чувствах здесь нет. Можно дать общению немного воздуха и посмотреть, появится ли с его стороны встречный шаг». В полном ответе раскрой именно выпавшие карты. Не достраивай за людей биографию и мотивы: «карта связана с дистанцией» допустимо, «он ушёл в себя, потому что ты мешала» — выдумка.
        """;

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
        sb.AppendLine("Ответь на вопрос через эти карты. Пиши связно, конкретно и без повторяющихся оговорок. Не объясняй пользователю его психологию и не придумывай поступки других людей.");
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
