namespace FutureViewer.DomainServices.Interfaces;

public interface ITelegramLinkUrlBuilder
{
    string BuildDeepLink(string token);
}
