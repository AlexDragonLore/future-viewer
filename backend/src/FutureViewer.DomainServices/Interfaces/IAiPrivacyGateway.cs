using FutureViewer.DomainServices.DTOs;

namespace FutureViewer.DomainServices.Interfaces;

public interface IAiPrivacyGateway
{
    AiPrivacyDecision Prepare(string? text, AiPrivacyOperation operation);
}
