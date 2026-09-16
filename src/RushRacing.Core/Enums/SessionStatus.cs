namespace RushRacing.Core.Enums;

public enum SessionStatus
{
    Created,
    PendingPayment,
    Paid,
    Starting,
    Active,
    Completing,
    Completed,
    PaymentFailed,
    MachineError,
    Cancelled,
    Refunded,
    Expired,
    AbandonedNoSelection,
    AbandonedPayment
}