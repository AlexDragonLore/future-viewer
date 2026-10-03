namespace FutureViewer.DomainServices.DTOs;

public sealed class UserPromptContext
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public int? BirthYear { get; init; }
    public required DateOnly Today { get; init; }
    public string? ClientTimeZone { get; init; }
    public required IReadOnlyList<string> MemoryRules { get; init; }
}
