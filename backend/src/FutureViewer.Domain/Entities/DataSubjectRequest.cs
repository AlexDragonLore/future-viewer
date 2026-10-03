using FutureViewer.Domain.Enums;

namespace FutureViewer.Domain.Entities;

public sealed class DataSubjectRequest
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public required Guid SubjectReference { get; init; }
    public required DataSubjectRequestType RequestType { get; init; }
    public required DateTime ReceivedAt { get; init; }
    public DateTime? IdentityVerifiedAt { get; set; }
    public required DateTime DueAt { get; init; }
    public DataSubjectRequestStatus Status { get; set; } = DataSubjectRequestStatus.Received;
    public DateTime? CompletedAt { get; set; }
    public string? ResultReference { get; set; }
}
