using FutureViewer.Domain.Enums;

namespace FutureViewer.Domain.Entities;

public sealed class UserConsent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public required Guid SubjectReference { get; init; }
    public required ConsentType ConsentType { get; init; }
    public required Guid LegalDocumentId { get; init; }
    public LegalDocument? LegalDocument { get; init; }
    public required string DocumentVersion { get; init; }
    public required string ContentHash { get; init; }
    public required DateTime AcceptedAt { get; init; }
    public DateTime? RevokedAt { get; set; }
    public required string CollectionSource { get; init; }
    public string? IpHash { get; init; }
    public string? UserAgentHash { get; init; }
}
