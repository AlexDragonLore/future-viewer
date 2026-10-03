using System.Text.RegularExpressions;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.Services;

namespace FutureViewer.Infrastructure.AI;

/// <summary>
/// A conservative local-only score. Feedback text is not transmitted to an AI
/// provider. The result is deliberately modest and must not infer personality or
/// sincerity from sensitive characteristics.
/// </summary>
public sealed partial class PrivacyPreservingFeedbackScorer : IFeedbackScorer
{
    public Task<FeedbackScoringResult> ScoreAsync(
        string question,
        string interpretation,
        string selfReport,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _ = question;
        _ = interpretation;

        var text = AiPrivacyGateway.Normalize(selfReport);
        var wordCount = WordRegex().Matches(text).Count;
        var hasConcreteAction = ActionRegex().IsMatch(text);
        var isSincereEnoughForGamification = wordCount >= 8;
        var score = !isSincereEnoughForGamification
            ? 1
            : Math.Clamp(3 + Math.Min(wordCount / 12, 4) + (hasConcreteAction ? 2 : 0), 1, 10);

        return Task.FromResult(new FeedbackScoringResult
        {
            Score = score,
            IsSincere = isSincereEnoughForGamification,
            Reason = isSincereEnoughForGamification
                ? "Самоотчёт принят локальной проверкой полноты; внешнему AI он не передавался."
                : "Для оценки не хватает конкретного описания действий; внешнему AI текст не передавался."
        });
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"\b(?:сделал|сделала|попробовал|попробовала|обсудил|обсудила|записал|записала|изменил|изменила|решил|решила|поговорил|поговорила|начал|начала|перестал|перестала)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ActionRegex();
}
