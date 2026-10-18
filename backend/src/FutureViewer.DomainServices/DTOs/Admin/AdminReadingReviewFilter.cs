namespace FutureViewer.DomainServices.DTOs.Admin;

public sealed record AdminReadingReviewFilter(Guid AdminId, bool UnseenOnly = false, bool OldestFirst = false);
