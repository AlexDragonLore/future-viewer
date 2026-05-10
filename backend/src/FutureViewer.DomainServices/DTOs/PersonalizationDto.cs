namespace FutureViewer.DomainServices.DTOs;

public sealed class PersonalizationDto
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public DateOnly? BirthDate { get; init; }
    public bool IsComplete { get; init; }
    public required IReadOnlyList<UserMemoryRuleDto> MemoryRules { get; init; }
}

public sealed class UserMemoryRuleDto
{
    public required Guid Id { get; init; }
    public required string Text { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
