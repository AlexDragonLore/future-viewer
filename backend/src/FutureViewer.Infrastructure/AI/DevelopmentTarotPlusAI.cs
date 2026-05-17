using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.Infrastructure.AI;

public sealed class DevelopmentTarotPlusAI : ITarotPlusAI
{
    private static readonly string[] IntakeQuestions =
    {
        "Что в этой теме сейчас вызывает самое сильное напряжение?",
        "Какой результат ты считаешь реалистичным и желанным?",
        "Что уже пробовал(а), и почему это не сработало полностью?",
        "Какая внутренняя или внешняя граница здесь важна?",
        "Какой ресурс у тебя уже есть, но используется не полностью?",
        "Чего ты опасаешься, если ситуация начнёт меняться?",
        "По каким признакам через месяц ты поймёшь, что стало лучше?",
        "Какой первый шаг кажется маленьким, но честным?"
    };

    public string Model => "development-tarot-plus-ai";

    public Task<TarotPlusRouteResult> RouteAsync(
        TarotPlusInterviewContext context,
        CancellationToken ct = default)
    {
        return Task.FromResult(new TarotPlusRouteResult
        {
            Route = GuessRoute(context.CoreRequest),
            PreviewText =
                $"Запрос «{context.CoreRequest}» подходит для глубокого жизненного компаса. " +
                "В полной сессии я уточню контекст, затем соберу несколько раскладов и оформлю практические ориентиры на 7, 30 и 90 дней.",
            SafetyFlags = Array.Empty<string>()
        });
    }

    public Task<TarotPlusQuestionResult> NextQuestionAsync(
        TarotPlusInterviewContext context,
        CancellationToken ct = default)
    {
        if (context.IntakeAnswerCount >= TarotPlusServiceLimits.MinIntakeAnswers)
        {
            return Task.FromResult(new TarotPlusQuestionResult
            {
                ReadyForReport = true,
                Question = null,
                SafetyFlags = Array.Empty<string>()
            });
        }

        return Task.FromResult(new TarotPlusQuestionResult
        {
            ReadyForReport = false,
            Question = IntakeQuestions[Math.Clamp(context.IntakeAnswerCount, 0, IntakeQuestions.Length - 1)],
            SafetyFlags = Array.Empty<string>()
        });
    }

    public Task<TarotPlusReportResult> GenerateReportAsync(
        TarotPlusReportContext context,
        CancellationToken ct = default)
    {
        var spreadLines = context.DrawnSpreads
            .Select(spread =>
                $"- **{spread.SpreadName}**: {string.Join(", ", spread.Cards.Take(3).Select(c => $"{c.PositionName} — {c.CardName}{(c.IsReversed ? " (пер.)" : string.Empty)}"))}")
            .ToArray();

        var report = $"""
        # Жизненный компас

        ## Главная тема
        Сейчас главный запрос звучит так: «{context.CoreRequest}». Этот локальный отчёт создан development-режимом, чтобы проверить сценарий без внешнего AI.

        ## Что показывают карты
        {string.Join("\n", spreadLines)}

        ## Слепые зоны и ресурсы
        Слепая зона может быть в попытке решить всё сразу. Ресурс уже есть в сформулированном запросе: тема названа достаточно конкретно, чтобы перейти к действиям.

        ## Рекомендации на 7 дней
        Выбери один маленький шаг и зафиксируй критерий, по которому поймёшь, что стало легче.

        ## Рекомендации на 30 дней
        Вернись к ответам интервью, отметь повторяющиеся мотивы и убери одно действие, которое больше не поддерживает цель.

        ## Рекомендации на 90 дней
        Собери устойчивый ритм: один еженедельный обзор, один разговор с собой без спешки и один практический шаг в выбранной сфере.

        ## Вопросы для самопроверки
        - Что стало яснее после формулировки запроса?
        - Какой шаг выглядит маленьким, но честным?
        """;

        return Task.FromResult(new TarotPlusReportResult { ReportMarkdown = report });
    }

    public Task<TarotPlusFollowUpResult> AskFollowUpAsync(
        TarotPlusFollowUpContext context,
        CancellationToken ct = default)
    {
        var answer = $"""
        В локальном режиме я отвечаю без внешнего AI, но сохраняю структуру Tarot+.

        Твой вопрос: **{context.Question}**.

        Смотри на него через главный запрос «{context.CoreRequest}»: что здесь можно проверить действием, а что пока остаётся предположением?

        Практический шаг: выбери один наблюдаемый признак прогресса и вернись к нему через неделю.
        """;

        return Task.FromResult(new TarotPlusFollowUpResult { AnswerMarkdown = answer });
    }

    private static TarotPlusRoute GuessRoute(string request)
    {
        var normalized = request.ToLowerInvariant();
        if (normalized.Contains("работ") || normalized.Contains("карьер")) return TarotPlusRoute.Career;
        if (normalized.Contains("деньг") || normalized.Contains("финанс")) return TarotPlusRoute.Money;
        if (normalized.Contains("отнош") || normalized.Contains("любов")) return TarotPlusRoute.Relationship;
        if (normalized.Contains("сем")) return TarotPlusRoute.Family;
        if (normalized.Contains("реш")) return TarotPlusRoute.Decision;
        return TarotPlusRoute.GeneralLife;
    }

    private static class TarotPlusServiceLimits
    {
        public const int MinIntakeAnswers = 5;
    }
}
