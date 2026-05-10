using FutureViewer.Domain.Entities;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.DomainServices.Services;

public sealed class PersonalizationService
{
    public const int MemoryLimit = 20;

    private readonly IUserRepository _users;
    private readonly IUserMemoryRepository _memory;

    public PersonalizationService(IUserRepository users, IUserMemoryRepository memory)
    {
        _users = users;
        _memory = memory;
    }

    public async Task<PersonalizationDto> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await GetUserAsync(userId, ct);
        var rules = await _memory.GetByUserAsync(userId, MemoryLimit, ct);
        return Map(user, rules);
    }

    public async Task<PersonalizationDto> UpdateAsync(
        Guid userId,
        UpdatePersonalizationRequest request,
        CancellationToken ct = default)
    {
        var user = await GetUserAsync(userId, ct);
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.BirthDate = request.BirthDate;
        await _users.UpdateAsync(user, ct);

        var rules = await _memory.GetByUserAsync(userId, MemoryLimit, ct);
        return Map(user, rules);
    }

    public async Task<UserPromptContext> GetPromptContextAsync(
        Guid userId,
        DateOnly? clientDate,
        string? clientTimeZone,
        CancellationToken ct = default)
    {
        var user = await GetUserAsync(userId, ct);
        if (!IsComplete(user))
            throw new ProfileRequiredException("Заполните имя, фамилию и дату рождения перед раскладом.");

        var rules = await _memory.GetByUserAsync(userId, MemoryLimit, ct);
        return new UserPromptContext
        {
            FirstName = user.FirstName!.Trim(),
            LastName = user.LastName!.Trim(),
            BirthDate = user.BirthDate!.Value,
            Today = clientDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            ClientTimeZone = string.IsNullOrWhiteSpace(clientTimeZone) ? null : clientTimeZone.Trim(),
            MemoryRules = rules.Select(r => r.Text).ToList()
        };
    }

    public async Task SaveExtractedMemoryAsync(
        Guid userId,
        IReadOnlyList<string> extractedRules,
        CancellationToken ct = default)
    {
        if (extractedRules.Count == 0) return;

        var existing = await _memory.GetByUserAsync(userId, MemoryLimit, ct);
        var normalizedExisting = existing
            .Select(r => Normalize(r.Text))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var text in extractedRules.Select(CleanRule).Where(x => x.Length > 0))
        {
            var normalized = Normalize(text);
            if (!normalizedExisting.Add(normalized)) continue;

            await _memory.AddAsync(new UserMemoryRule
            {
                UserId = userId,
                Text = text
            }, ct);
        }

        await _memory.DeleteOldestBeyondLimitAsync(userId, MemoryLimit, ct);
    }

    public Task<bool> DeleteMemoryRuleAsync(Guid userId, Guid id, CancellationToken ct = default) =>
        _memory.DeleteAsync(userId, id, ct);

    public Task DeleteAllMemoryAsync(Guid userId, CancellationToken ct = default) =>
        _memory.DeleteAllAsync(userId, ct);

    private async Task<User> GetUserAsync(Guid userId, CancellationToken ct)
    {
        return await _users.GetByIdAsync(userId, ct)
               ?? throw new NotFoundException("User not found");
    }

    private static PersonalizationDto Map(User user, IReadOnlyList<UserMemoryRule> rules)
    {
        return new PersonalizationDto
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            BirthDate = user.BirthDate,
            IsComplete = IsComplete(user),
            MemoryRules = rules.Select(r => new UserMemoryRuleDto
            {
                Id = r.Id,
                Text = r.Text,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            }).ToList()
        };
    }

    private static bool IsComplete(User user) =>
        !string.IsNullOrWhiteSpace(user.FirstName)
        && !string.IsNullOrWhiteSpace(user.LastName)
        && user.BirthDate.HasValue;

    private static string CleanRule(string text) =>
        text.Trim().Length > 500 ? text.Trim()[..500] : text.Trim();

    private static string Normalize(string text) =>
        string.Join(' ', text.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
