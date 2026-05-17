using FutureViewer.DomainServices.DTOs;

namespace FutureViewer.DomainServices.Interfaces;

public interface ITarotPlusAI
{
    string Model { get; }

    Task<TarotPlusRouteResult> RouteAsync(
        TarotPlusInterviewContext context,
        CancellationToken ct = default);

    Task<TarotPlusQuestionResult> NextQuestionAsync(
        TarotPlusInterviewContext context,
        CancellationToken ct = default);

    Task<TarotPlusReportResult> GenerateReportAsync(
        TarotPlusReportContext context,
        CancellationToken ct = default);

    Task<TarotPlusFollowUpResult> AskFollowUpAsync(
        TarotPlusFollowUpContext context,
        CancellationToken ct = default);
}
