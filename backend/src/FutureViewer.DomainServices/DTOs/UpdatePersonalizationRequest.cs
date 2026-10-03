namespace FutureViewer.DomainServices.DTOs;

public sealed class UpdatePersonalizationRequest
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public int? BirthYear { get; init; }
}
