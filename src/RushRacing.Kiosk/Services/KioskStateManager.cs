using RushRacing.Kiosk.Enums;

namespace RushRacing.Kiosk.Services;

public class KioskStateManager
{
    private KioskState _currentState = KioskState.Initializing;
    private readonly object _lock = new();

    public event Action<KioskState, KioskState>? StateChanged;

    public KioskState CurrentState
    {
        get { lock (_lock) return _currentState; }
    }

    public void TransitionTo(KioskState newState)
    {
        KioskState oldState;
        lock (_lock)
        {
            if (_currentState == newState) return;
            oldState = _currentState;
            _currentState = newState;
        }

        System.Diagnostics.Debug.WriteLine($"[KIOSK STATE] {oldState} → {newState}");
        StateChanged?.Invoke(oldState, newState);
    }
}