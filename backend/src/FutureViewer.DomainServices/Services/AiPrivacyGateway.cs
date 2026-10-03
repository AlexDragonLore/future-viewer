using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FutureViewer.DomainServices.Services;

/// <summary>
/// The only policy boundary for user-controlled text before an external AI call.
/// It is deliberately deterministic: sensitive and high-stakes requests never need
/// to leave the application in order to decide that they must be blocked.
/// </summary>
public sealed partial class AiPrivacyGateway : IAiPrivacyGateway
{
    public const int MaxQuestionLength = 500;
    public const int MaxFeedbackLength = 2_000;
    public const int MaxDerivedTextLength = 12_000;

    public const string SensitiveDataMessage =
        "Вопрос содержит сведения, которые могут относиться к персональным или чувствительным данным. " +
        "Уберите имена, контакты, диагнозы, адреса и сведения, позволяющие определить конкретного человека.";

    private const string GeneralSafetyMessage =
        "Сформулируйте вопрос без имён, контактов и сведений о других людях — как размышление о собственных чувствах, вариантах и безопасных следующих шагах.";

    private readonly ILogger<AiPrivacyGateway> _logger;

    public AiPrivacyGateway(ILogger<AiPrivacyGateway>? logger = null)
    {
        _logger = logger ?? NullLogger<AiPrivacyGateway>.Instance;
    }

    public AiPrivacyDecision Prepare(string? text, AiPrivacyOperation operation)
    {
        var requestId = Guid.NewGuid();
        var normalized = Normalize(text);
        var maxLength = operation switch
        {
            AiPrivacyOperation.QuestionValidation or AiPrivacyOperation.TarotInterpretation => MaxQuestionLength,
            AiPrivacyOperation.FeedbackScoring => MaxFeedbackLength,
            _ => MaxDerivedTextLength
        };

        if (normalized.Length == 0)
            return Block(requestId, operation, "empty", "Введите вопрос без персональных данных.");

        if (normalized.Length > maxLength)
            return Block(
                requestId,
                operation,
                "text_too_long",
                $"Сократите текст до {maxLength} символов и удалите из него персональные данные.");

        // High-stakes routes are handled locally. The original text is never sent to AI.
        if (SelfHarmRegex().IsMatch(normalized))
            return Block(
                requestId,
                operation,
                "self_harm",
                "Если опасность непосредственная, позвоните 112 или 103 и постарайтесь не оставаться в одиночестве. " +
                "Свяжитесь с человеком, которому доверяете, или с врачом. Таро и AI не подходят для помощи в кризисе.");

        if (UrgentMedicalRegex().IsMatch(normalized))
            return Block(
                requestId,
                operation,
                "urgent_medical",
                "При срочных симптомах позвоните 112 или 103. Не откладывайте обращение за медицинской помощью ради расклада или ответа AI.");

        if (ViolenceRegex().IsMatch(normalized))
            return Block(
                requestId,
                operation,
                "violence",
                "Если вам или другому человеку угрожает опасность, перейдите в безопасное место и позвоните 112. " +
                "Сервис не даёт инструкций, связанных с насилием.");

        if (MedicationRegex().IsMatch(normalized))
            return Block(
                requestId,
                operation,
                "medication",
                "Не начинайте, не отменяйте и не меняйте дозировку лекарства по раскладу или ответу AI. Обратитесь к врачу или фармацевту; при срочных симптомах — 112 или 103.");

        if (CrimeRegex().IsMatch(normalized))
            return Block(
                requestId,
                operation,
                "crime",
                "Сервис не помогает совершать или скрывать преступления. При непосредственной угрозе жизни или безопасности позвоните 112; для правовой оценки обратитесь к адвокату.");

        if (FinancialDecisionRegex().IsMatch(normalized))
            return Block(
                requestId,
                operation,
                "financial_decision",
                "Не принимайте решение об инвестиции, кредите или переводе денег на основании Таро или AI. " +
                "Проверьте условия и риски и при необходимости обратитесь к квалифицированному финансовому специалисту.");

        if (LegalDecisionRegex().IsMatch(normalized))
            return Block(
                requestId,
                operation,
                "legal_decision",
                "Таро и AI не заменяют юридическую консультацию. До подписания, подачи заявления, отказа от права или иного юридически значимого действия обратитесь к адвокату, нотариусу или компетентному органу.");

        if (SpecialCategoryRegex().IsMatch(normalized))
            return Block(requestId, operation, "special_category", SensitiveDataMessage);

        if (MinorRegex().IsMatch(normalized))
            return Block(requestId, operation, "minor_data", SensitiveDataMessage);

        if (ThirdPartyRegex().IsMatch(normalized) || FullNameRegex().IsMatch(normalized))
            return Block(requestId, operation, "third_party_or_name", SensitiveDataMessage);

        if (EmailRegex().IsMatch(normalized))
            return Block(requestId, operation, "email", SensitiveDataMessage);

        if (PhoneRegex().IsMatch(normalized))
            return Block(requestId, operation, "phone", SensitiveDataMessage);

        if (AddressRegex().IsMatch(normalized))
            return Block(requestId, operation, "address", SensitiveDataMessage);

        if (DocumentRegex().IsMatch(normalized))
            return Block(requestId, operation, "identity_document", SensitiveDataMessage);

        if (BankDetailsRegex().IsMatch(normalized) || LooksLikePaymentCard(normalized))
            return Block(requestId, operation, "bank_details", SensitiveDataMessage);

        if (AccountHandleRegex().IsMatch(normalized))
            return Block(requestId, operation, "account_identifier", SensitiveDataMessage);

        if (PromptInjectionRegex().IsMatch(normalized))
            return Block(
                requestId,
                operation,
                "prompt_injection",
                "Сформулируйте только вопрос для интерпретации, без инструкций системе или попыток изменить правила сервиса.");

        return Record(new AiPrivacyDecision
        {
            RequestId = requestId,
            Operation = operation,
            Disposition = AiPrivacyDisposition.Allowed,
            SafeText = normalized,
            ReasonCode = "allowed",
            UserMessage = "Вопрос прошёл локальную проверку данных."
        });
    }

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = text.Normalize(NormalizationForm.FormKC);
        var sb = new StringBuilder(normalized.Length);
        foreach (var rune in normalized.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate)
            {
                sb.Append(' ');
                continue;
            }

            sb.Append(rune.ToString());
        }

        return WhitespaceRegex().Replace(sb.ToString(), " ").Trim();
    }

    private AiPrivacyDecision Block(
        Guid requestId,
        AiPrivacyOperation operation,
        string reasonCode,
        string safeResponse)
    {
        return Record(new AiPrivacyDecision
        {
            RequestId = requestId,
            Operation = operation,
            Disposition = AiPrivacyDisposition.Blocked,
            SafeText = string.Empty,
            ReasonCode = reasonCode,
            UserMessage = reasonCode is "email" or "phone" or "address" or "identity_document" or
                "bank_details" or "account_identifier" or "third_party_or_name" or
                "special_category" or "minor_data"
                    ? SensitiveDataMessage
                    : GeneralSafetyMessage,
            SafeResponse = safeResponse
        });
    }

    private AiPrivacyDecision Record(AiPrivacyDecision decision)
    {
        // Deliberately content-free. Do not add input, output, user, email or endpoint here.
        _logger.LogInformation(
            "AI privacy decision requestId={RequestId} operation={Operation} disposition={Disposition} reason={ReasonCode}",
            decision.RequestId,
            decision.Operation,
            decision.Disposition,
            decision.ReasonCode);
        return decision;
    }

    private static bool LooksLikePaymentCard(string value)
    {
        foreach (Match match in CardCandidateRegex().Matches(value))
        {
            var digits = new string(match.Value.Where(char.IsDigit).ToArray());
            if (digits.Length is >= 13 and <= 19 && PassesLuhn(digits))
                return true;
        }

        return false;
    }

    private static bool PassesLuhn(string digits)
    {
        var sum = 0;
        var alternate = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var n = digits[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }

            sum += n;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}._%+-])[\p{L}\p{N}._%+-]+@[\p{L}\p{N}.-]+\.[\p{L}]{2,}(?![\p{L}\p{N}.-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?<!\d)(?:\+?7|8)?[\s()\-.]*\d{3}[\s()\-.]*\d{3}[\s\-.]*\d{2}[\s\-.]*\d{2}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"\b(?:адрес|улиц[аеы]?|ул\.|проспект|пр-т|переулок|пер\.|дом|д\.|квартир[аеы]?|кв\.)\s+[\p{L}\d][\p{L}\d\s.,/-]{2,80}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AddressRegex();

    [GeneratedRegex(@"\b(?:паспорт|серия\s+и\s+номер|снилс|инн|полис\s+(?:омс|дмс)|водительск(?:ое|ого)\s+удостоверени[ея]|номер\s+документа)\b|(?<!\d)\d{3}-\d{3}-\d{3}[ -]?\d{2}(?!\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DocumentRegex();

    [GeneratedRegex(@"\b(?:расч[её]тн(?:ый|ого)\s+сч[её]т|корреспондентск(?:ий|ого)\s+сч[её]т|бик|iban|swift|cvv|cvc|пин[- ]?код|номер\s+карт[ыа])\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BankDetailsRegex();

    [GeneratedRegex(@"(?<!\d)(?:\d[ -]?){13,19}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex CardCandidateRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])(?:@[a-z0-9_]{3,32}|https?://\S+|t\.me/\S+|vk\.com/\S+)(?![\p{L}\p{N}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AccountHandleRegex();

    [GeneratedRegex(@"(?<!\p{L})\p{Lu}\p{Ll}{1,30}\s+\p{Lu}\p{Ll}{1,30}(?:\s+\p{Lu}\p{Ll}{1,30})?(?!\p{L})", RegexOptions.CultureInvariant)]
    private static partial Regex FullNameRegex();

    [GeneratedRegex(@"\b(?:мо(?:й|я|его|ей|и|их|ему|им|ю)|наш\p{L}*|его|е[её]|их)\s+(?:муж\p{L}*|жен[аыуе]|жены|партн[её]р\p{L}*|реб[её]н\p{L}*|сын\p{L}*|доч\p{L}*|мат(?:ь|ери|ерью)|от(?:ец|ца|цу|цом|це)|врач\p{L}*|начальник\p{L}*|коллег\p{L}*|сосед\p{L}*|друг\p{L}*|подруг\p{L}*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ThirdPartyRegex();

    // Russian stems must consume their inflection before the final word boundary;
    // a boundary directly after "беремен" or "депресси" never matches ordinary words.
    [GeneratedRegex(@"\b(?:(?:диагноз|болезн|онколог|психиатр|депресси|беремен|выкидыш|аборт|интимн|сексуальн|религи|вероисповед|мусульман|христиан|иуде|политическ|оппозици|национальност|этническ)\p{L}*|рак(?:а|у|ом|е)?|вич|спид|рас[аые]|уголовн\p{L}*\s+(?:дел\p{L}*|обвинени\p{L}*|стать\p{L}*))\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SpecialCategoryRegex();

    [GeneratedRegex(@"\b(?:несовершеннолетн\p{L}*|малолетн\p{L}*|реб[её]н\p{L}*|подрост\p{L}*|школьник\p{L}*|школьниц\p{L}*|сыну\s+\d{1,2}|дочери\s+\d{1,2})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MinorRegex();

    [GeneratedRegex(@"\b(?:(?:самоубий|суицид|самоповреж)\p{L}*|покончить\s+с\s+собой|не\s+хочу\s+жить|убить\s+себя|вскрыть\s+вен\p{L}*|прыгнуть\s+с\s+крыши)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SelfHarmRegex();

    [GeneratedRegex(@"\b(?:не\s+дыш\p{L}*|потерял[аи]?\s+сознани\p{L}*|сильн(?:ая|ое)\s+(?:кровотечени\p{L}*|боль)|инфаркт\p{L}*|инсульт\p{L}*|передозировк\p{L}*|анафилак\p{L}*|срочн(?:ая|о)\s+медицин\p{L}*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrgentMedicalRegex();

    [GeneratedRegex(@"\b(?:убить|избить|напасть|(?:насили|оружи|угрожа|заминиров)\p{L}*|причинить\s+вред|взорвать)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ViolenceRegex();

    [GeneratedRegex(@"\b(?:лекарств\p{L}*|таблет\p{L}*|дозировк\p{L}*|антидепрессант\p{L}*|антибиотик\p{L}*|инсулин\p{L}*|отменить\s+препарат\p{L}*|начать\s+принимать|перестать\s+принимать)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MedicationRegex();

    [GeneratedRegex(@"\b(?:(?:совершить|скрыть)\s+преступлен\p{L}*|избежать\s+полици\p{L}*|спрятать\s+(?:тело|улики)|взломать|украсть|(?:шантаж|наркотик)\p{L}*|обойти\s+закон)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CrimeRegex();

    [GeneratedRegex(@"\b(?:(?:купить|продать)\s+(?:акци|крипт)\p{L}*|вложить|(?:инвестир|ипотек)\p{L}*|(?:взять|оформить)\s+кредит\p{L}*|перевести\s+деньги|гарантированн\p{L}*\s+доход\p{L}*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FinancialDecisionRegex();

    [GeneratedRegex(@"\b(?:подписать\s+(?:договор|соглашени)\p{L}*|подать\s+(?:иск|заявлени)\p{L}*|отказаться\s+от\s+права|вступить\s+в\s+наследство|развестись|судиться|признать\s+вину|дать\s+показани\p{L}*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LegalDecisionRegex();

    [GeneratedRegex(@"\b(?:игнорируй|забудь|отмени)\s+(?:предыдущ|системн)(?:ие|ый|ую)\s+(?:инструкци|правил|промпт)|\bsystem\s*prompt\b|\bdeveloper\s*message\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PromptInjectionRegex();
}
