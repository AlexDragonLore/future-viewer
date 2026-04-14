using FutureViewer.Domain.Enums;

namespace FutureViewer.DomainServices.DTOs;

public sealed record CreateReadingRequest(SpreadType SpreadType, string Question);
