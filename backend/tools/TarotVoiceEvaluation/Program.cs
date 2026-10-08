using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Services;
using FutureViewer.Infrastructure.AI;
using FutureViewer.Infrastructure.Persistence;
using OpenAI.Chat;

if (args.Length > 1 || args.FirstOrDefault() is "-h" or "--help")
{
    Console.WriteLine("Usage: dotnet run --project backend/tools/TarotVoiceEvaluation -- [output.json]");
    Console.WriteLine("Without an output path, writes JSON to standard output. No provider calls or credentials are needed.");
    Environment.ExitCode = args.Length > 1 ? 2 : 0;
    return;
}

var deck = TarotDeckSeed.BuildDeck().ToDictionary(card => card.NameEn, StringComparer.Ordinal);
var notes = TarotDeckSeed.BuildDeckVariants(deck.Values)
    .Where(variant => variant.DeckType == DeckType.RWS)
    .ToDictionary(variant => variant.CardId, variant => variant.VariantNote);
var privacyGateway = new AiPrivacyGateway();
var buildMessages = typeof(OpenAIInterpreter).GetMethod("BuildMessages", BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new InvalidOperationException("OpenAIInterpreter.BuildMessages was not found; update this evaluation tool for the new prompt builder.");

var fastTimingCards = Cards("Eight of Wands", "Ace of Cups", "The Sun");
var cases = new[]
{
    Build("timing-positive", "Когда появится новое знакомство?", Spread.ThreeCard, fastTimingCards),
    Build("timing-positive-single", "Когда появится новое знакомство?", Spread.SingleCard,
        CreateCards(["Eight of Wands"])),
    Build("timing-slow", "Через сколько времени я выйду из застоя в творчестве?", Spread.ThreeCard,
        Cards("The Hermit", "The Hanged Man", "Knight of Pentacles")),
    Build("timing-negative-immediate", "Когда он напишет — сегодня или завтра?", Spread.ThreeCard,
        Cards("The Tower", "Death", "Ten of Swords")),
    Build("relationship-mixed", "Я ему надоела?", Spread.ThreeCard,
        Cards("The Hermit", "Five of Pentacles", "Seven of Cups", reversedPositions: [1])),
    Build("reunion-negative", "Мы снова будем вместе?", Spread.ThreeCard,
        Cards("The Tower", "Death", "Two of Cups", reversedPositions: [2])),
    Build("reunion-negative-single", "Мы снова будем вместе?", Spread.SingleCard,
        CreateCards(["Ten of Swords"])),
    Build("clear-choice", "Что сейчас выбрать: продолжать старый творческий проект или начать новый?", Spread.ThreeCard,
        Cards("Eight of Pentacles", "Two of Wands", "Ace of Wands", reversedPositions: [0])),
    Build("existing-period", "Что ждёт меня в работе на этой неделе?", Spread.ThreeCard,
        Cards("Ten of Wands", "Eight of Pentacles", "Six of Wands")),
    Build("large-spread", "Как будет развиваться мой творческий проект и где его самая сильная сторона?", Spread.CelticCross,
        CardsForLargeSpread()),
    Build("timing-positive-stream", "Когда появится новое знакомство?", Spread.ThreeCard, fastTimingCards, stream: true),
};

var json = JsonSerializer.Serialize(cases, new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
});

if (args.Length == 0)
{
    Console.WriteLine(json);
}
else
{
    var outputPath = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    File.WriteAllText(outputPath, json + Environment.NewLine);
    Console.WriteLine($"Wrote {cases.Length} synthetic cases to {outputPath}");
}

EvaluationCase Build(string id, string question, Spread spread, IReadOnlyList<ReadingCard> cards, bool stream = false)
{
    if (cards.Count != spread.CardCount)
        throw new InvalidOperationException($"Synthetic case '{id}' must supply every spread position.");

    var privacy = privacyGateway.Prepare(question, AiPrivacyOperation.TarotInterpretation);
    if (!privacy.CanSendExternally)
        throw new InvalidOperationException($"Synthetic case '{id}' failed the local privacy check: {privacy.ReasonCode}.");

    // A fixed, entirely synthetic technical ID keeps baseline and candidate exports comparable.
    var requestId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    var messages = (IReadOnlyList<ChatMessage>)(buildMessages.Invoke(null,
        [spread, privacy.SafeText, cards, DeckType.RWS, notes, requestId])
        ?? throw new InvalidOperationException("The prompt builder returned no messages."));

    return new EvaluationCase(id, privacy.SafeText, messages.Select(message => new EvaluationMessage(
        message is SystemChatMessage ? "system" : message is UserChatMessage ? "user" : throw new InvalidOperationException("Unexpected prompt message role."),
        string.Concat(message.Content.Select(part => part.Text)))).ToArray(), stream);
}

IReadOnlyList<ReadingCard> Cards(string first, string second, string third, IReadOnlyList<int>? reversedPositions = null) =>
    CreateCards([first, second, third], reversedPositions);

IReadOnlyList<ReadingCard> CardsForLargeSpread() => CreateCards(
    ["The Magician", "Eight of Swords", "Queen of Wands", "Ten of Wands", "The Star",
     "Three of Pentacles", "Page of Wands", "Six of Pentacles", "The Moon", "The Sun"]);

IReadOnlyList<ReadingCard> CreateCards(IReadOnlyList<string> names, IReadOnlyList<int>? reversedPositions = null) =>
    names.Select((name, position) => new ReadingCard
    {
        CardId = deck[name].Id,
        Card = deck[name],
        Position = position,
        IsReversed = reversedPositions?.Contains(position) == true,
    }).ToArray();

sealed record EvaluationCase(string Id, string Question, IReadOnlyList<EvaluationMessage> Messages, bool Stream);
sealed record EvaluationMessage(string Role, string Content);
