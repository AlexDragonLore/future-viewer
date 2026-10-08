using System.Text.RegularExpressions;
using FutureViewer.DomainServices.DTOs;

namespace FutureViewer.DomainServices.Services;

public static partial class QuestionValidationHeuristics
{
    public static string BuildFallbackSuggestion(string question)
    {
        var trimmed = question.Trim();
        if (trimmed.Length == 0)
            return "На что мне сейчас стоит обратить внимание?";

        if (trimmed.Length <= 40)
            return $"Что мне важно понять про тему \"{trimmed}\"?";

        return "Какой следующий шаг мне стоит увидеть в этой ситуации?";
    }

    public static QuestionValidationResult? TryValidate(string question)
    {
        var trimmed = question.Trim();
        if (trimmed.Length == 0)
            return NeedsRewrite("Вопрос пустой.", "На что мне сейчас стоит обратить внимание?");

        var normalized = WhitespaceRegex().Replace(trimmed.ToLowerInvariant(), " ");
        if (LooksLikeGibberish(normalized))
            return Rejected(
                "Вопрос выглядит как случайный набор символов.",
                "На что мне сейчас стоит обратить внимание?");

        if (DangerousRegex().IsMatch(normalized))
            return Rejected(
                "Этот вопрос требует медицинского, юридического, финансового или опасного совета.",
                "Как мне бережно позаботиться о себе и выбрать следующий безопасный шаг?");

        if (ExactFactRegex().IsMatch(normalized))
            return NeedsRewrite(
                "Таро не подходит для точных фактов, дат, номеров или гарантированных прогнозов.",
                "На какие возможности и риски мне стоит обратить внимание в этой ситуации?");

        if (ControlRegex().IsMatch(normalized))
            return Rejected(
                "Вопрос фокусируется на контроле другого человека.",
                "Что мне важно понять о своих чувствах и дальнейших действиях в этой ситуации?");

        if (SurveillanceRegex().IsMatch(normalized))
            return NeedsRewrite(
                "Лучше не требовать точного факта о мыслях или действиях другого человека.",
                "На что мне обратить внимание в этих отношениях и как бережно прояснить ситуацию?");

        var timingRequest = NegatedDatePrecisionRegex().Replace(normalized, "");
        if (ExactDateRegex().IsMatch(timingRequest) || WhenExactlyRegex().IsMatch(timingRequest))
            return NeedsRewrite(
                "По картам можно дать ориентировочный срок, без точной даты и гарантий.",
                BuildApproximateTimingSuggestion(trimmed));

        if (TooVagueRegex().IsMatch(normalized))
            return NeedsRewrite(
                "Вопрос слишком общий, без темы или ситуации.",
                "На что мне сейчас стоит обратить внимание в любви, работе или личном выборе?");

        if (IsSingleVagueWord(normalized))
            return NeedsRewrite(
                "Одного слова мало для полезного расклада.",
                $"Что мне важно понять про тему \"{trimmed}\"?");

        return null;
    }

    private static string BuildApproximateTimingSuggestion(string question)
    {
        // Preserve the subject of the privacy-checked question while removing
        // its demand for precision. Unrelated exact facts are handled above.
        var approximate = ExactDateRegex().Replace(question, match =>
            $"примерн{match.Groups["ending"].Value.ToLowerInvariant()} {match.Groups["date"].Value.ToLowerInvariant()}");
        approximate = ExactTimingWordRegex().Replace(approximate, "примерно");
        approximate = WhitespaceRegex().Replace(approximate, " ").Trim().TrimEnd('?', '.', '!');
        return $"{approximate}? Укажи ориентировочный диапазон в днях, неделях или месяцах как символический ориентир.";
    }

    private static bool LooksLikeGibberish(string text)
    {
        var lettersOnly = LettersOnlyRegex().Replace(text, "");
        if (lettersOnly.Length < 2)
            return true;

        if (KeyboardMashRegex().IsMatch(text))
            return true;

        var hasSpace = text.Contains(' ');
        var hasTarotTopic = TopicRegex().IsMatch(text);
        return !hasSpace && !hasTarotTopic && lettersOnly.Length is >= 5 and <= 12;
    }

    private static bool IsSingleVagueWord(string text)
    {
        if (text.Contains(' '))
            return false;
        return VagueSingleWordRegex().IsMatch(text);
    }

    private static QuestionValidationResult NeedsRewrite(string reason, string suggestedQuestion) => new()
    {
        Status = QuestionValidationStatus.NeedsRewrite,
        Reason = reason,
        SuggestedQuestion = suggestedQuestion
    };

    private static QuestionValidationResult Rejected(string reason, string? suggestedQuestion = null) => new()
    {
        Status = QuestionValidationStatus.Rejected,
        Reason = reason,
        SuggestedQuestion = suggestedQuestion
    };

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[^\p{L}]+", RegexOptions.Compiled)]
    private static partial Regex LettersOnlyRegex();

    [GeneratedRegex(@"^(ыва|фыв|asdf|qwer|йцу|цук|ваы|ываыва|фыва|хз|лол)+$", RegexOptions.Compiled)]
    private static partial Regex KeyboardMashRegex();

    [GeneratedRegex(@"(отнош|любов|работ|карьер|деньг|финанс|выбор|семь|партнер|партнёр|будущ|переезд|учеб|здоров|самочувств|состояни|чувств|действ|возможност|риск)", RegexOptions.Compiled)]
    private static partial Regex TopicRegex();

    [GeneratedRegex(@"^(любовь|работа|деньги|отношения|будущее|карьера|семья|учеба|учёба|здоровье)$", RegexOptions.Compiled)]
    private static partial Regex VagueSingleWordRegex();

    [GeneratedRegex(@"(рак|диагноз|болезн|беремен|лечени|таблет|операци|суд|иск|адвокат|законно|посадят|вложить все|инвестировать все|кредит на все|финансов(ая|ую) гаранти|убить|самоуб|суицид|навредить)", RegexOptions.Compiled)]
    private static partial Regex DangerousRegex();

    [GeneratedRegex(@"(какой номер|номер выигра|лотере|выиграю ли|гарантирован|100%|сто процентов)", RegexOptions.Compiled)]
    private static partial Regex ExactFactRegex();

    [GeneratedRegex(@"\bточн(?<ending>ая|ую|ой|ою|ые|ых|ым|ыми)\s+(?<date>дат(?:а|у|е|ы|ой|ою|ам|ами|ах)?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExactDateRegex();

    [GeneratedRegex(@"\b(?:без(?:\s+указания)?|не)\s+точн\p{L}*\s+дат\p{L}*\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NegatedDatePrecisionRegex();

    [GeneratedRegex(@"\bкогда\b[^?!.\n]*\b(?:точно|именно)\b|\b(?:точно|именно)\s+когда\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WhenExactlyRegex();

    [GeneratedRegex(@"\b(?:точно|именно)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExactTimingWordRegex();

    [GeneratedRegex(@"(как заставить|как вынудить|как принудить|как вернуть любой ценой|приворот|манипулир)", RegexOptions.Compiled)]
    private static partial Regex ControlRegex();

    [GeneratedRegex(@"(изменяет ли .*точно|следит ли|проверить .*телефон|что он думает точно|что она думает точно|любит ли .*точно)", RegexOptions.Compiled)]
    private static partial Regex SurveillanceRegex();

    [GeneratedRegex(@"^(что будет\??|ну как там\??|скажи все\??|скажи всё\??|что по кайфу\??|что там\??|как оно\??)$", RegexOptions.Compiled)]
    private static partial Regex TooVagueRegex();
}
