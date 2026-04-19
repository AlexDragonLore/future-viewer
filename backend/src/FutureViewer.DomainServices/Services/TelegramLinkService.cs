using System.Security.Cryptography;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.DomainServices.Services;

public sealed class TelegramLinkService
{
    private readonly IUserRepository _users;
    private readonly ITelegramLinkUrlBuilder _urlBuilder;

    public TelegramLinkService(IUserRepository users, ITelegramLinkUrlBuilder urlBuilder)
    {
        _users = users;
        _urlBuilder = urlBuilder;
    }

    public async Task<TelegramLinkResponse> GenerateLinkAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User not found");

        if (user.TelegramChatId.HasValue)
        {
            return new TelegramLinkResponse
            {
                DeepLinkUrl = string.Empty,
                IsLinked = true
            };
        }

        var token = GenerateToken();
        user.TelegramLinkToken = token;
        await _users.UpdateAsync(user, ct);

        return new TelegramLinkResponse
        {
            DeepLinkUrl = _urlBuilder.BuildDeepLink(token),
            IsLinked = false
        };
    }

    public async Task<bool> CompleteLinkAsync(string token, long chatId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var user = await _users.GetByLinkTokenAsync(token, ct);
        if (user is null) return false;

        user.TelegramChatId = chatId;
        user.TelegramLinkToken = null;
        await _users.UpdateAsync(user, ct);
        return true;
    }

    public async Task UnlinkAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User not found");
        user.TelegramChatId = null;
        user.TelegramLinkToken = null;
        await _users.UpdateAsync(user, ct);
    }

    public async Task<bool> IsLinkedAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User not found");
        return user.TelegramChatId.HasValue;
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", string.Empty);
    }
}
