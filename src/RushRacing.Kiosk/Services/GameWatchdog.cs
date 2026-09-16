using RushRacing.Kiosk.Models;
using System.Diagnostics;

namespace RushRacing.Kiosk.Services;

public class GameWatchdog
{
    private readonly GameLauncher _gameLauncher;
    private readonly int _maxCrashes;
    private readonly TimeSpan _crashWindow;

    private int _crashCount = 0;
    private DateTime _crashWindowStart = DateTime.UtcNow;

    public event Action<int>? OnGameCrashed;
    public event Action<string>? OnMaxCrashesReached;

    public GameWatchdog(GameLauncher gameLauncher, int maxCrashes, int crashWindowMinutes)
    {
        _gameLauncher = gameLauncher;
        _maxCrashes = maxCrashes;
        _crashWindow = TimeSpan.FromMinutes(crashWindowMinutes);
    }

    public async Task StartWatching(string gameId, CancellationToken ct)
    {
        _crashCount = 0;
        _crashWindowStart = DateTime.UtcNow;

        System.Diagnostics.Debug.WriteLine($"[WATCHDOG] Watching game: {gameId}");

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(2000, ct);

            if (!_gameLauncher.IsGameRunning(gameId))
            {
                System.Diagnostics.Debug.WriteLine($"[WATCHDOG] Game not running!");

                // Reset crash window if expired
                if (DateTime.UtcNow - _crashWindowStart > _crashWindow)
                {
                    _crashCount = 0;
                    _crashWindowStart = DateTime.UtcNow;
                }

                _crashCount++;
                System.Diagnostics.Debug.WriteLine($"[WATCHDOG] Crash #{_crashCount}");

                OnGameCrashed?.Invoke(_crashCount);

                if (_crashCount >= _maxCrashes)
                {
                    OnMaxCrashesReached?.Invoke(
                        $"Game crashed {_crashCount} times within {_crashWindow.TotalMinutes} minutes");
                    return;
                }

                // Wait a moment then restart
                await Task.Delay(3000, ct);

                System.Diagnostics.Debug.WriteLine($"[WATCHDOG] Restarting game...");
                var launched = await _gameLauncher.LaunchGame(gameId);

                if (!launched)
                {
                    OnMaxCrashesReached?.Invoke("Game failed to restart after crash");
                    return;
                }
            }
        }
    }

    public void Reset()
    {
        _crashCount = 0;
        _crashWindowStart = DateTime.UtcNow;
    }
}