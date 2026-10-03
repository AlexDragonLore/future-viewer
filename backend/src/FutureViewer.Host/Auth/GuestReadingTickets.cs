using System.Security.Cryptography;
using System.Text.Json;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using Microsoft.AspNetCore.DataProtection;

namespace FutureViewer.Host.Auth;

// The browser keeps only this encrypted continuation ticket, never the hidden text.
// A minimized operational reading row remains in the database for atomic ownership.
public sealed class GuestReadingTickets(IDataProtectionProvider provider)
{
    private readonly ITimeLimitedDataProtector _protector = provider
        .CreateProtector("FutureViewer.GuestReading.v1").ToTimeLimitedDataProtector();

    public GuestReadingResponse Issue(ReadingResult reading)
    {
        if (string.IsNullOrWhiteSpace(reading.Interpretation))
            throw new InvalidOperationException("The guest interpretation is empty.");
        var expiresAt = DateTimeOffset.UtcNow.AddHours(24);
        var ticket = _protector.Protect(JsonSerializer.Serialize(reading), expiresAt);
        return new(Preview(reading), ticket, expiresAt);
    }

    public ReadingResult Read(string? ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket) || ticket.Length > 128_000)
            throw Unavailable();
        try
        {
            return JsonSerializer.Deserialize<ReadingResult>(_protector.Unprotect(ticket)) ?? throw Unavailable();
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
        {
            throw Unavailable();
        }
    }

    public static ReadingResult Preview(ReadingResult reading)
    {
        var text = reading.Interpretation ?? string.Empty;
        var end = text.Length / 2;
        // Stop at a word boundary near the midpoint, without ever returning the second half.
        var space = text.LastIndexOf(' ', Math.Max(0, end - 1), end);
        if (space > end / 2) end = space;
        if (end > 0 && char.IsHighSurrogate(text[end - 1])) end--;
        return new ReadingResult
        {
            Id = reading.Id,
            SpreadType = reading.SpreadType,
            SpreadName = reading.SpreadName,
            Question = reading.Question,
            CreatedAt = reading.CreatedAt,
            DeckType = reading.DeckType,
            Cards = reading.Cards,
            Interpretation = text[..end].TrimEnd() + "…",
            IsPreview = true
        };
    }

    private static NotFoundException Unavailable()
        => new("Срок хранения расклада истёк или ссылка недействительна. Начните новый расклад.");
}

public sealed record GuestReadingResponse(ReadingResult Reading, string Ticket, DateTimeOffset ExpiresAt);
public sealed record GuestReadingTicketRequest(string Ticket);
