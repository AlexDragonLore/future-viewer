using FutureViewer.Domain.Enums;

namespace FutureViewer.Domain.Entities;

public sealed class TarotPlusSession
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid UserId { get; init; }
    public User? User { get; init; }

    public TarotPlusSessionStatus Status { get; set; } = TarotPlusSessionStatus.Draft;
    public TarotPlusRoute Route { get; set; } = TarotPlusRoute.Unknown;

    public string CoreRequest { get; set; } = string.Empty;
    public string? PreviewText { get; set; }

    public string AnswersJson { get; set; } = "[]";
    public string SelectedSpreadsJson { get; set; } = "[]";
    public string DrawnCardsJson { get; set; } = "[]";

    public string? ReportMarkdown { get; set; }
    public string? AiModel { get; set; }

    public int FollowUpsLeft { get; set; } = 2;
    public string FollowUpsJson { get; set; } = "[]";

    public string? PaymentId { get; set; }
    public decimal PriceRub { get; set; } = 100m;
    public DateTime? PaidAt { get; set; }

    public string SafetyFlagsJson { get; set; } = "[]";

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(30);
}
