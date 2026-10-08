using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.Extensions.Logging;

namespace FutureViewer.DomainServices.Services;

public sealed class SubscriptionService
{
    public const int FreeDailyLimit = 1;
    public const int SubscriptionDurationDays = 30;
    public const decimal NpdAnnualIncomeLimitRub = 2_400_000m;
    public bool IsPaymentConfigured => _payments.IsConfigured;

    private readonly IUserRepository _users;
    private readonly IReadingRepository _readings;
    private readonly IPaymentProvider _payments;
    private readonly IProcessedPaymentRepository _processedPayments;
    private readonly IUnitOfWork _uow;
    private readonly IPaymentOrderRepository? _orders;
    private readonly ILogger<SubscriptionService>? _logger;
    private readonly IPrivacyRepository? _privacy;

    public SubscriptionService(
        IUserRepository users,
        IReadingRepository readings,
        IPaymentProvider payments,
        IProcessedPaymentRepository processedPayments,
        IUnitOfWork uow,
        IPaymentOrderRepository? orders = null,
        ILogger<SubscriptionService>? logger = null,
        IPrivacyRepository? privacy = null)
    {
        _users = users;
        _readings = readings;
        _payments = payments;
        _processedPayments = processedPayments;
        _uow = uow;
        _orders = orders;
        _logger = logger;
        _privacy = privacy;
    }

    public async Task EnsureReadingAllowedAsync(Guid userId, SpreadType spreadType, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new UnauthorizedException("User not found");

        if (IsSubscriptionActive(user))
            return;

        var isIntroReading = spreadType == SpreadType.ThreeCard && !user.HasUsedIntroReading
            && await _readings.CountByUserAsync(userId, ct) == 0;
        if (spreadType != SpreadType.SingleCard && !isIntroReading)
            throw new SubscriptionRequiredException(
                "Первый расклад на три карты бесплатный. После него бесплатно доступна только одна карта в день.");

        var count = await GetReadingsUsedTodayAsync(user, ct);
        if (count >= FreeDailyLimit)
            throw new QuotaExceededException(
                $"Daily free reading limit reached ({FreeDailyLimit}). Subscribe for unlimited access.");
    }

    public Task<Reading> AddReadingAsync(Reading reading, CancellationToken ct = default)
    {
        var userId = reading.UserId ?? throw new UnauthorizedException("Authentication required");
        return _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            // Serialize quota checks and persistence, then release the lock before AI generation.
            await _users.LockAsync(userId, innerCt);
            await EnsureReadingAllowedAsync(userId, reading.SpreadType, innerCt);
            var saved = await _readings.AddAsync(reading, innerCt);
            await RecordReadingUsageAsync(userId, reading.CreatedAt, innerCt);
            return saved;
        }, ct);
    }

    public Task<bool> AttachGuestAsync(ReadingResult reading, Guid userId, CancellationToken ct = default) =>
        _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _users.LockAsync(userId, innerCt);
            var stored = await _readings.GetByIdAsync(reading.Id, innerCt);
            if (stored is null || (stored.UserId is not null && stored.UserId != userId)
                || stored.DeletedFromHistoryAt is not null)
                return false;

            // A retry of the same claim is idempotent; another guest reading consumes access.
            if (stored.UserId is null)
                await EnsureReadingAllowedAsync(userId, reading.SpreadType, innerCt);
            var attached = await _readings.AttachGuestAsync(
                reading.Id, userId, reading.Question, reading.Interpretation, innerCt);
            if (attached)
                await RecordReadingUsageAsync(userId, reading.CreatedAt, innerCt);
            return attached;
        }, ct);

    private async Task RecordReadingUsageAsync(Guid userId, DateTime createdAt, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new UnauthorizedException("User not found");
        if (user.HasUsedIntroReading && user.LastReadingAt >= createdAt) return;
        user.HasUsedIntroReading = true;
        if (user.LastReadingAt is null || user.LastReadingAt < createdAt)
            user.LastReadingAt = createdAt;
        await _users.UpdateAsync(user, ct);
    }

    private async Task<int> GetReadingsUsedTodayAsync(User user, CancellationToken ct)
    {
        var count = await _readings.CountTodayByUserAsync(user.Id, ct);
        // Deleting reading content must not reset the daily allowance.
        return Math.Max(count, user.LastReadingAt?.Date == DateTime.UtcNow.Date ? 1 : 0);
    }

    public async Task<SubscriptionStatusDto> GetStatusAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new UnauthorizedException("User not found");

        var isActive = IsSubscriptionActive(user);
        var usedToday = await GetReadingsUsedTodayAsync(user, ct);
        var canCreateIntroReading = !isActive && !user.HasUsedIntroReading
            && await _readings.CountByUserAsync(userId, ct) == 0;

        return new SubscriptionStatusDto
        {
            Status = user.SubscriptionStatus,
            ExpiresAt = user.SubscriptionExpiresAt,
            IsActive = isActive,
            FreeReadingsUsedToday = usedToday,
            FreeReadingsDailyLimit = FreeDailyLimit,
            CanCreateFreeReading = isActive || usedToday < FreeDailyLimit,
            CanCreateIntroReading = canCreateIntroReading
        };
    }

    public async Task<bool> HasActiveSubscriptionAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new UnauthorizedException("User not found");

        return IsSubscriptionActive(user);
    }

    public async Task<PaymentCreationDto> CreatePaymentAsync(
        Guid userId,
        CreatePaymentRequest request,
        CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new UnauthorizedException("User not found");

        if (_orders is null || _privacy is null)
            throw new InvalidOperationException("Local payment order and consent storage are required.");
        if (!request.OfferAccepted || string.IsNullOrWhiteSpace(request.OfferVersion))
            throw new DomainException("Для оплаты необходимо принять действующую оферту.");
        var offer = await _privacy.GetActiveLegalDocumentAsync(
            LegalDocumentType.PublicOffer, request.OfferVersion, ct)
            ?? throw new ConflictException("Оферта обновилась. Обновите страницу и ознакомьтесь с действующей редакцией.");

        var product = _payments.Products.FirstOrDefault(product =>
            string.Equals(product.TariffCode, request.TariffCode, StringComparison.Ordinal))
            ?? throw new DomainException("Выбранный тариф недоступен. Обновите страницу и выберите действующий тариф.");
        if (product.Amount <= 0 || decimal.Round(product.Amount, 2) != product.Amount || product.AccessDays <= 0
            || string.IsNullOrWhiteSpace(product.TariffCode)
            || !string.Equals(product.Currency, "RUB", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Payment product is not configured.");

        var order = new PaymentOrder
        {
            UserId = user.Id,
            SubjectReference = user.PrivacySubjectId,
            TariffCode = product.TariffCode,
            Amount = product.Amount,
            Currency = product.Currency.ToUpperInvariant(),
            AccessDays = product.AccessDays,
            Provider = _payments.ProviderName,
            IdempotencyKey = Guid.NewGuid().ToString("N")
        };
        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            await _orders.AddAsync(order, innerCt);
            await _privacy.AddConsentAsync(new UserConsent
            {
                UserId = user.Id,
                SubjectReference = user.PrivacySubjectId,
                ConsentType = ConsentType.OfferAcceptance,
                LegalDocumentId = offer.Id,
                DocumentVersion = offer.Version,
                ContentHash = offer.ContentHash,
                AcceptedAt = DateTime.UtcNow,
                CollectionSource = $"checkout:{order.PublicId:N}"
            }, innerCt);
            return true;
        }, ct);

        PaymentCreationResult result;
        try
        {
            result = await _payments.CreateSubscriptionPaymentAsync(
                order.PublicId,
                order.IdempotencyKey,
                product,
                ct);
            order.ProviderPaymentId = result.PaymentId;
            order.Status = PaymentOrderStatus.ProviderCreated;
            await _orders.UpdateAsync(order, ct);
        }
        catch
        {
            order.Status = PaymentOrderStatus.Failed;
            await _orders.UpdateAsync(order, ct);
            throw;
        }

        return new PaymentCreationDto
        {
            PaymentId = order.PublicId.ToString("N"),
            ConfirmationUrl = result.ConfirmationUrl,
            Status = result.Status
        };
    }

    public async Task<PaymentStatusDto> GetPaymentStatusAsync(
        Guid userId, Guid publicOrderId, CancellationToken ct = default)
    {
        var order = _orders is null ? null : await _orders.GetByPublicIdAsync(publicOrderId, ct);
        if (order?.UserId != userId)
            throw new NotFoundException("Платёж не найден.");
        return new PaymentStatusDto(order.Status.ToString().ToLowerInvariant(), order.Status == PaymentOrderStatus.Paid);
    }

    public async Task<bool> ProcessWebhookAsync(string body, CancellationToken ct = default)
    {
        return await ProcessWebhookWithOutcomeAsync(body, ct) == PaymentWebhookHandling.Processed;
    }

    public bool IsWebhookSourceAllowed(string? sourceAddress) =>
        _payments.IsWebhookSourceAllowed(sourceAddress);

    public async Task<PaymentWebhookHandling> ProcessWebhookWithOutcomeAsync(
        string body,
        CancellationToken ct = default)
    {
        if (_orders is null) return PaymentWebhookHandling.Rejected;

        var evt = _payments.ParseWebhook(body);
        if (evt is null) return PaymentWebhookHandling.Rejected;
        if (evt.Type == PaymentWebhookEventType.PaymentCanceled)
            return PaymentWebhookHandling.Acknowledged;
        if (evt.Type != PaymentWebhookEventType.PaymentSucceeded)
            return PaymentWebhookHandling.Rejected;
        if (string.IsNullOrEmpty(evt.PaymentId)) return PaymentWebhookHandling.Rejected;

        var verified = await _payments.VerifyPaymentAsync(evt.PaymentId, ct);
        if (verified is null) return PaymentWebhookHandling.Rejected;
        if (!string.Equals(verified.PaymentId, evt.PaymentId, StringComparison.Ordinal))
            return PaymentWebhookHandling.Rejected;
        if (!verified.Paid) return PaymentWebhookHandling.Rejected;
        if (!string.Equals(verified.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
            return PaymentWebhookHandling.Rejected;
        if (verified.Amount is null || string.IsNullOrWhiteSpace(verified.Currency))
            return PaymentWebhookHandling.Rejected;
        if (verified.OrderId is null)
        {
            // Previously completed payments predate local checkout orders. A
            // provider-verified replay only acknowledges the existing receipt;
            // unknown legacy payments cannot grant access or create an order.
            return await _processedPayments.ExistsAsync(verified.PaymentId, ct)
                ? PaymentWebhookHandling.Acknowledged
                : PaymentWebhookHandling.Rejected;
        }

        var outcome = await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            var order = await _orders.GetByPublicIdForUpdateAsync(verified.OrderId.Value, innerCt);
            if (order is null
                || order.UserId is null
                || order.Status is PaymentOrderStatus.Canceled or PaymentOrderStatus.Failed
                || !string.Equals(order.Provider, _payments.ProviderName, StringComparison.Ordinal)
                || order.Amount <= 0 || order.AccessDays <= 0
                || order.Amount != verified.Amount.Value
                || !string.Equals(order.Currency, verified.Currency, StringComparison.OrdinalIgnoreCase)
                || (order.ProviderPaymentId is not null
                    && !string.Equals(order.ProviderPaymentId, verified.PaymentId, StringComparison.Ordinal)))
            {
                return PaymentWebhookHandling.Rejected;
            }

            // Serialize grants for one owner so separate paid orders cannot overwrite
            // each other's access. Read the expiry only after acquiring the lock.
            await _orders.LockOwnerAsync(order.UserId.Value, innerCt);
            var user = await _users.GetByIdAsync(order.UserId.Value, innerCt);
            if (user is null) return PaymentWebhookHandling.Rejected;
            if (!await _processedPayments.TryRecordAsync(verified.PaymentId, user.Id, innerCt))
                return PaymentWebhookHandling.Acknowledged;

            // Honor the price and access term saved at checkout, even after repricing.
            var now = DateTime.UtcNow;
            var currentExpiry = user.SubscriptionExpiresAt is { } e && e > now ? e : now;
            user.SubscriptionStatus = SubscriptionStatus.Active;
            user.SubscriptionExpiresAt = currentExpiry.AddDays(order.AccessDays);
            user.YukassaSubscriptionId = verified.PaymentId;

            await _users.UpdateAsync(user, innerCt);
            order.ProviderPaymentId = verified.PaymentId;
            order.Status = PaymentOrderStatus.Paid;
            order.CompletedAt = now;
            order.ReceiptStatus = NpdReceiptStatus.PendingManualIssue;
            await _orders.UpdateAsync(order, innerCt);
            return PaymentWebhookHandling.Processed;
        }, ct);

        if (outcome == PaymentWebhookHandling.Processed)
        {
            var now = DateTime.UtcNow;
            var total = await _orders.GetPaidTotalAsync(
                new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(now.Year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ct);
            if (total >= NpdAnnualIncomeLimitRub * 0.8m)
            {
                _logger?.LogWarning(
                    "NPD annual income threshold warning: totalRub={TotalRub}; thresholdRatio={ThresholdRatio}; receiptStatus={ReceiptStatus}",
                    total,
                    0.8m,
                    NpdReceiptStatus.PendingManualIssue);
            }
        }

        return outcome;
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

public enum PaymentWebhookHandling
{
    Rejected = 0,
    Acknowledged = 1,
    Processed = 2
}
