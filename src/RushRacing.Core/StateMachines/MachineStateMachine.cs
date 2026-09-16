using RushRacing.Core.Enums;

namespace RushRacing.Core.StateMachines;

public static class MachineStateMachine
{
    private static readonly Dictionary<MachineStatus, HashSet<MachineStatus>> ValidTransitions = new()
    {
        [MachineStatus.Offline] = new()
        {
            MachineStatus.Booting,
            MachineStatus.Disabled
        },
        [MachineStatus.Booting] = new()
        {
            MachineStatus.Ready,
            MachineStatus.Error,
            MachineStatus.Offline
        },
        [MachineStatus.Ready] = new()
        {
            MachineStatus.Reserved,
            MachineStatus.Maintenance,
            MachineStatus.Disabled,
            MachineStatus.Offline,
            MachineStatus.Error
        },
        [MachineStatus.Reserved] = new()
        {
            MachineStatus.GameSelect,
            MachineStatus.Error,
            MachineStatus.Ready,
            MachineStatus.Offline
        },
        [MachineStatus.GameSelect] = new()
        {
            MachineStatus.Starting,
            MachineStatus.Ready,       // Abandoned selection
            MachineStatus.Error,
            MachineStatus.Offline
        },
        [MachineStatus.Starting] = new()
        {
            MachineStatus.InSession,
            MachineStatus.Error,
            MachineStatus.Offline
        },
        [MachineStatus.InSession] = new()
        {
            MachineStatus.Resetting,
            MachineStatus.Error,
            MachineStatus.Offline
        },
        [MachineStatus.Resetting] = new()
        {
            MachineStatus.Ready,
            MachineStatus.Error,
            MachineStatus.Offline
        },
        [MachineStatus.Error] = new()
        {
            MachineStatus.Ready,
            MachineStatus.Maintenance,
            MachineStatus.Disabled,
            MachineStatus.Offline
        },
        [MachineStatus.Maintenance] = new()
        {
            MachineStatus.Ready,
            MachineStatus.Disabled,
            MachineStatus.Offline
        },
        [MachineStatus.Disabled] = new()
        {
            MachineStatus.Offline,
            MachineStatus.Maintenance
        }
    };

    public static bool CanTransition(MachineStatus from, MachineStatus to)
    {
        return ValidTransitions.ContainsKey(from)
            && ValidTransitions[from].Contains(to);
    }

    public static void ValidateTransition(MachineStatus from, MachineStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException(
                $"Invalid machine state transition: {from} → {to}");
        }
    }
}