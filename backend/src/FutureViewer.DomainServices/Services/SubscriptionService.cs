using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.DomainServices.Services;

public sealed class SubscriptionService
{
    public const int FreeDailyLimit = 1;

    private readonly IUserRepository _users;
    private readonly IReadingRepository _readings;

    public SubscriptionService(IUserRepository users, IReadingRepository readings)
    {
        _users = users;
        _readings = readings;
    }

    public async Task EnsureReadingAllowedAsync(Guid userId, SpreadType spreadType, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new UnauthorizedException("User not found");

        if (IsSubscriptionActive(user))
            return;

        if (spreadType != SpreadType.SingleCard)
            throw new SubscriptionRequiredException(
                "Active subscription required for this spread type. Free tier supports only single-card readings.");

        var count = await _readings.CountTodayByUserAsync(userId, ct);
        if (count >= FreeDailyLimit)
            throw new QuotaExceededException(
                $"Daily free reading limit reached ({FreeDailyLimit}). Subscribe for unlimited access.");
    }

    public async Task<SubscriptionStatusDto> GetStatusAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new UnauthorizedException("User not found");

        var isActive = IsSubscriptionActive(user);
        var usedToday = await _readings.CountTodayByUserAsync(userId, ct);

        return new SubscriptionStatusDto
        {
            Status = user.SubscriptionStatus,
            ExpiresAt = user.SubscriptionExpiresAt,
            IsActive = isActive,
            FreeReadingsUsedToday = usedToday,
            FreeReadingsDailyLimit = FreeDailyLimit,
            CanCreateFreeReading = isActive || usedToday < FreeDailyLimit
        };
    }

    private static bool IsSubscriptionActive(User user)
    {
        if (user.SubscriptionStatus != SubscriptionStatus.Active)
            return false;
        if (user.SubscriptionExpiresAt is null)
            return false;
        return user.SubscriptionExpiresAt.Value > DateTime.UtcNow;
    }
}
