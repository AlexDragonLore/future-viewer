using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.DomainServices.Services;

public sealed class SubscriptionService
{
    public const int FreeDailyLimit = 1;
    public const int SubscriptionDurationDays = 30;

    private readonly IUserRepository _users;
    private readonly IReadingRepository _readings;
    private readonly IPaymentProvider _payments;
    private readonly IProcessedPaymentRepository _processedPayments;
    private readonly IUnitOfWork _uow;

    public SubscriptionService(
        IUserRepository users,
        IReadingRepository readings,
        IPaymentProvider payments,
        IProcessedPaymentRepository processedPayments,
        IUnitOfWork uow)
    {
        _users = users;
        _readings = readings;
        _payments = payments;
        _processedPayments = processedPayments;
        _uow = uow;
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

    public async Task<PaymentCreationDto> CreatePaymentAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new UnauthorizedException("User not found");

        if (IsSubscriptionActive(user))
            throw new SubscriptionAlreadyActiveException(
                "Subscription is already active. It will auto-renew at expiry.");

        var result = await _payments.CreateSubscriptionPaymentAsync(userId, user.Email, ct);

        return new PaymentCreationDto
        {
            PaymentId = result.PaymentId,
            ConfirmationUrl = result.ConfirmationUrl,
            Status = result.Status
        };
    }

    public async Task<bool> ProcessWebhookAsync(string body, CancellationToken ct = default)
    {
        var evt = _payments.ParseWebhook(body);
        if (evt is null) return false;
        if (evt.Type != PaymentWebhookEventType.PaymentSucceeded) return false;
        if (string.IsNullOrEmpty(evt.PaymentId)) return false;

        var verified = await _payments.VerifyPaymentAsync(evt.PaymentId, ct);
        if (verified is null) return false;
        if (!verified.Paid) return false;
        if (!string.Equals(verified.Status, "succeeded", StringComparison.OrdinalIgnoreCase)) return false;
        if (verified.UserId is null) return false;

        var user = await _users.GetByIdAsync(verified.UserId.Value, ct);
        if (user is null) return false;

        return await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            if (!await _processedPayments.TryRecordAsync(verified.PaymentId, user.Id, innerCt))
                return false;

            var now = DateTime.UtcNow;
            var currentExpiry = user.SubscriptionExpiresAt is { } e && e > now ? e : now;
            user.SubscriptionStatus = SubscriptionStatus.Active;
            user.SubscriptionExpiresAt = currentExpiry.AddDays(SubscriptionDurationDays);
            user.YukassaSubscriptionId = verified.PaymentId;

            await _users.UpdateAsync(user, innerCt);
            return true;
        }, ct);
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
