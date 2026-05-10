namespace FutureViewer.DomainServices.DTOs;

public sealed class UserPromptContext
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required DateOnly BirthDate { get; init; }
    public required DateOnly Today { get; init; }
    public string? ClientTimeZone { get; init; }
    public required IReadOnlyList<string> MemoryRules { get; init; }
}
