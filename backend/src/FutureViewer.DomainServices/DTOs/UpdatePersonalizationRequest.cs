namespace FutureViewer.DomainServices.DTOs;

public sealed class UpdatePersonalizationRequest
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required DateOnly BirthDate { get; init; }
}
