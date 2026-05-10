using System.Text.RegularExpressions;
using FutureViewer.DomainServices.DTOs;

namespace FutureViewer.DomainServices.Services;

public static partial class QuestionValidationHeuristics
{
    public static QuestionValidationResult? TryValidate(string question)
    {
        var trimmed = question.Trim();
        if (trimmed.Length == 0)
            return NeedsRewrite("Вопрос пустой.", "На что мне сейчас стоит обратить внимание?");

        var normalized = WhitespaceRegex().Replace(trimmed.ToLowerInvariant(), " ");
        if (LooksLikeGibberish(normalized))
            return Rejected("Вопрос выглядит как случайный набор символов.");

        if (DangerousRegex().IsMatch(normalized))
            return Rejected("Этот вопрос требует медицинского, юридического, финансового или опасного совета.");

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

    [GeneratedRegex(@"(точн(ая|ую|ый|ое)? дат|когда .*точно|какой номер|номер выигра|лотере|выиграю ли|гарантирован|100%|сто процентов)", RegexOptions.Compiled)]
    private static partial Regex ExactFactRegex();

    [GeneratedRegex(@"(как заставить|как вынудить|как принудить|как вернуть любой ценой|приворот|манипулир)", RegexOptions.Compiled)]
    private static partial Regex ControlRegex();

    [GeneratedRegex(@"(изменяет ли .*точно|следит ли|проверить .*телефон|что он думает точно|что она думает точно|любит ли .*точно)", RegexOptions.Compiled)]
    private static partial Regex SurveillanceRegex();

    [GeneratedRegex(@"^(что будет\??|ну как там\??|скажи все\??|скажи всё\??|что по кайфу\??|что там\??|как оно\??)$", RegexOptions.Compiled)]
    private static partial Regex TooVagueRegex();
}
