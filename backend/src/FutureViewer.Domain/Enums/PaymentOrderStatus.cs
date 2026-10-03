namespace FutureViewer.Domain.Enums;

public enum PaymentOrderStatus
{
    Pending = 0,
    ProviderCreated = 1,
    Paid = 2,
    Canceled = 3,
    Failed = 4
}

public enum NpdReceiptStatus
{
    PendingManualIssue = 0,
    Issued = 1,
    Refunded = 2,
    NotRequired = 3
}
