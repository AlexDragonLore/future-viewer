namespace FutureViewer.DomainServices.DTOs;

public sealed class TelegramLinkResponse
{
    public required string DeepLinkUrl { get; init; }
    public required bool IsLinked { get; init; }
}
