using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.Integration.Tests.Fixtures;

public sealed class StubTarotPlusAI : ITarotPlusAI
{
    public string Model => "stub-tarot-plus";

    public Task<TarotPlusRouteResult> RouteAsync(TarotPlusInterviewContext context, CancellationToken ct = default) =>
        Task.FromResult(new TarotPlusRouteResult
        {
            Route = TarotPlusRoute.GeneralLife,
            PreviewText = "Stub Tarot+ preview",
            SafetyFlags = Array.Empty<string>()
        });

    public Task<TarotPlusQuestionResult> NextQuestionAsync(TarotPlusInterviewContext context, CancellationToken ct = default) =>
        Task.FromResult(new TarotPlusQuestionResult
        {
            Question = "Stub next question?",
            ReadyForReport = context.IntakeAnswerCount >= 5,
            SafetyFlags = Array.Empty<string>()
        });

    public Task<TarotPlusReportResult> GenerateReportAsync(TarotPlusReportContext context, CancellationToken ct = default) =>
        Task.FromResult(new TarotPlusReportResult
        {
            ReportMarkdown = "# Жизненный компас\n\nStub report."
        });

    public Task<TarotPlusFollowUpResult> AskFollowUpAsync(TarotPlusFollowUpContext context, CancellationToken ct = default) =>
        Task.FromResult(new TarotPlusFollowUpResult
        {
            AnswerMarkdown = "Stub follow-up answer."
        });
}
