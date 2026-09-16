using RushRacing.Core.Enums;

namespace RushRacing.Core.StateMachines;

public static class SessionStateMachine
{
    private static readonly Dictionary<SessionStatus, HashSet<SessionStatus>> ValidTransitions = new()
    {
        [SessionStatus.Created] = new()
        {
            SessionStatus.PendingPayment,
            SessionStatus.Cancelled
        },
        [SessionStatus.PendingPayment] = new()
        {
            SessionStatus.Paid,
            SessionStatus.PaymentFailed,
            SessionStatus.AbandonedPayment
        },
        [SessionStatus.Paid] = new()
        {
            SessionStatus.Starting,
            SessionStatus.MachineError,
            SessionStatus.Cancelled
        },
        [SessionStatus.Starting] = new()
        {
            SessionStatus.Active,
            SessionStatus.MachineError,
            SessionStatus.AbandonedNoSelection
        },
        [SessionStatus.Active] = new()
        {
            SessionStatus.Completing,
            SessionStatus.MachineError,
            SessionStatus.Expired
        },
        [SessionStatus.Completing] = new()
        {
            SessionStatus.Completed
        },
        [SessionStatus.MachineError] = new()
        {
            SessionStatus.Refunded,
            SessionStatus.Cancelled
        },
        [SessionStatus.AbandonedNoSelection] = new()
        {
            SessionStatus.Refunded,
            SessionStatus.Cancelled
        }
        // Terminal states: Completed, PaymentFailed, AbandonedPayment,
        //                  Cancelled, Refunded, Expired
    };

    public static bool CanTransition(SessionStatus from, SessionStatus to)
    {
        return ValidTransitions.ContainsKey(from)
            && ValidTransitions[from].Contains(to);
    }

    public static void ValidateTransition(SessionStatus from, SessionStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException(
                $"Invalid session state transition: {from} → {to}");
        }
    }

    public static bool IsTerminal(SessionStatus status)
    {
        return !ValidTransitions.ContainsKey(status);
    }
}