using FutureViewer.Domain.Enums;

namespace FutureViewer.DomainServices.DTOs;

public sealed class CreateTarotPlusPreviewRequest
{
    public required string CoreRequest { get; init; }
    public required string MainSphere { get; init; }
    public required string DesiredOutcome { get; init; }
}

public sealed class AddTarotPlusAnswerRequest
{
    public required string Question { get; init; }
    public required string Answer { get; init; }
}

public sealed class TarotPlusFollowUpRequest
{
    public required string Question { get; init; }
}

public sealed class TarotPlusPreviewDto
{
    public required TarotPlusSessionDto Session { get; init; }
    public required string PreviewText { get; init; }
    public required TarotPlusRoute Route { get; init; }
    public required string RouteLabel { get; init; }
}

public sealed class TarotPlusSessionDto
{
    public required Guid Id { get; init; }
    public required TarotPlusSessionStatus Status { get; init; }
    public required TarotPlusRoute Route { get; init; }
    public required string RouteLabel { get; init; }
    public required string CoreRequest { get; init; }
    public string? PreviewText { get; init; }
    public string? ReportMarkdown { get; init; }
    public required int FollowUpsLeft { get; init; }
    public required decimal PriceRub { get; init; }
    public DateTime? PaidAt { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required int AnswerCount { get; init; }
    public required int IntakeAnswerCount { get; init; }
    public required IReadOnlyList<TarotPlusAnswerDto> Answers { get; init; }
    public required IReadOnlyList<TarotPlusDrawnSpreadDto> DrawnSpreads { get; init; }
    public required IReadOnlyList<TarotPlusFollowUpItemDto> FollowUps { get; init; }
}

public sealed class TarotPlusSessionListItemDto
{
    public required Guid Id { get; init; }
    public required TarotPlusSessionStatus Status { get; init; }
    public required TarotPlusRoute Route { get; init; }
    public required string RouteLabel { get; init; }
    public required string CoreRequest { get; init; }
    public required decimal PriceRub { get; init; }
    public DateTime? PaidAt { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime ExpiresAt { get; init; }
}

public sealed class TarotPlusNextStepDto
{
    public required TarotPlusSessionStatus Status { get; init; }
    public string? Question { get; init; }
    public required bool CanGenerateReport { get; init; }
    public required int AnswerCount { get; init; }
    public required int RequiredAnswers { get; init; }
    public required int MaxAnswers { get; init; }
}

public sealed class TarotPlusReportDto
{
    public required TarotPlusSessionDto Session { get; init; }
    public required string ReportMarkdown { get; init; }
    public required IReadOnlyList<TarotPlusDrawnSpreadDto> DrawnSpreads { get; init; }
}

public sealed class TarotPlusFollowUpDto
{
    public required TarotPlusSessionDto Session { get; init; }
    public required string AnswerMarkdown { get; init; }
    public required int FollowUpsLeft { get; init; }
}

public sealed class TarotPlusAnswerDto
{
    public required string Kind { get; init; }
    public required string Question { get; init; }
    public required string Answer { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed class TarotPlusFollowUpItemDto
{
    public required string Question { get; init; }
    public required string AnswerMarkdown { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed class TarotPlusDrawnSpreadDto
{
    public required string SpreadId { get; init; }
    public required string SpreadName { get; init; }
    public required IReadOnlyList<TarotPlusDrawnCardDto> Cards { get; init; }
}

public sealed class TarotPlusDrawnCardDto
{
    public required int Position { get; init; }
    public required string PositionName { get; init; }
    public required int CardId { get; init; }
    public required string CardName { get; init; }
    public required string ImagePath { get; init; }
    public required bool IsReversed { get; init; }
    public required string Meaning { get; init; }
}

public sealed class TarotPlusInterviewContext
{
    public required string CoreRequest { get; init; }
    public required IReadOnlyList<TarotPlusAnswerDto> Answers { get; init; }
    public required TarotPlusRoute Route { get; init; }
    public required IReadOnlyList<string> SafetyFlags { get; init; }
    public required int IntakeAnswerCount { get; init; }
}

public sealed class TarotPlusRouteResult
{
    public required TarotPlusRoute Route { get; init; }
    public required string PreviewText { get; init; }
    public required IReadOnlyList<string> SafetyFlags { get; init; }
}

public sealed class TarotPlusQuestionResult
{
    public string? Question { get; init; }
    public required bool ReadyForReport { get; init; }
    public required IReadOnlyList<string> SafetyFlags { get; init; }
}

public sealed class TarotPlusReportContext
{
    public required string CoreRequest { get; init; }
    public required TarotPlusRoute Route { get; init; }
    public required IReadOnlyList<TarotPlusAnswerDto> Answers { get; init; }
    public required IReadOnlyList<TarotPlusDrawnSpreadDto> DrawnSpreads { get; init; }
    public required IReadOnlyList<string> SafetyFlags { get; init; }
}

public sealed class TarotPlusReportResult
{
    public required string ReportMarkdown { get; init; }
}

public sealed class TarotPlusFollowUpContext
{
    public required string CoreRequest { get; init; }
    public required TarotPlusRoute Route { get; init; }
    public required string ReportMarkdown { get; init; }
    public required IReadOnlyList<TarotPlusDrawnSpreadDto> DrawnSpreads { get; init; }
    public required IReadOnlyList<TarotPlusFollowUpItemDto> PreviousFollowUps { get; init; }
    public required string Question { get; init; }
}

public sealed class TarotPlusFollowUpResult
{
    public required string AnswerMarkdown { get; init; }
}
