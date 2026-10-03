using FutureViewer.Domain.Enums;

namespace FutureViewer.Domain.Entities;

public sealed class LegalDocument
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required LegalDocumentType DocumentType { get; init; }
    public required string Version { get; init; }
    public required string ContentHash { get; init; }
    public required DateTime PublishedAt { get; init; }
    public required DateTime EffectiveAt { get; init; }
    public bool IsActive { get; set; }
    public ICollection<UserConsent> Consents { get; init; } = new List<UserConsent>();
}
